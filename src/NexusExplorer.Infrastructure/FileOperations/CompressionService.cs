using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Infrastructure.FileOperations;

/// <summary>
/// Implements file compression using ZIP format with streaming I/O,
/// progress reporting, and cancellation support.
/// </summary>
public sealed class CompressionService : ICompressionService
{
    private readonly ILogger<CompressionService> _logger;
    private const int BufferSize = 1024 * 1024; // 1 MB buffer for streaming
    private const int ProgressThrottleMs = 100;  // throttle UI progress updates

    public CompressionService(ILogger<CompressionService> logger)
    {
        _logger = logger;
    }

    public async Task<CompressionResult> CompressAsync(
        IReadOnlyList<string> sourcePaths,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sourcePaths.Count == 0)
            return CompressionResult.Failed("No items selected for compression.");

        string? zipPath = null;
        try
        {
            // Determine output directory and ZIP name
            var firstPath = sourcePaths[0];
            var outputDirectory = Path.GetDirectoryName(firstPath)
                ?? throw new InvalidOperationException("Cannot determine output directory.");

            var zipName = GenerateZipName(sourcePaths);
            zipPath = GetUniqueZipPath(outputDirectory, zipName);

            // Calculate total size for progress reporting
            var entries = CollectEntries(sourcePaths, cancellationToken);
            var totalBytes = entries.Sum(e => e.Size);
            var totalItems = entries.Count;

            var stopwatch = Stopwatch.StartNew();
            long bytesProcessed = 0;
            var itemsProcessed = 0;
            long lastReportMs = -1;

            // Create ZIP with streaming
            await Task.Run(async () =>
            {
                using var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false);

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var zipEntry = archive.CreateEntry(entry.RelativePath, CompressionLevel.Optimal);

                    if (entry.IsDirectory)
                    {
                        // Directories are represented by entries ending with /
                        itemsProcessed++;
                        ReportProgress(progress, entry.RelativePath, itemsProcessed, totalItems, bytesProcessed, totalBytes, stopwatch);
                        continue;
                    }

                    // Stream file content into the ZIP entry
                    using var entryStream = zipEntry.Open();
                    using var fileStream = new FileStream(entry.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

                    var buffer = new byte[BufferSize];
                    int bytesRead;
                    while ((bytesRead = await fileStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await entryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        bytesProcessed += bytesRead;
                        // Throttle: at most one UI update every ~100ms so large files don't flood it.
                        if (ShouldReport(stopwatch, ref lastReportMs))
                            ReportProgress(progress, entry.RelativePath, itemsProcessed, totalItems, bytesProcessed, totalBytes, stopwatch);
                    }

                    itemsProcessed++;
                    ReportProgress(progress, entry.RelativePath, itemsProcessed, totalItems, bytesProcessed, totalBytes, stopwatch);
                }
            }, cancellationToken);

            // Report completion
            progress?.Report(new FileOperationProgress
            {
                OperationType = FileOperationType.Compress,
                CurrentItem = Path.GetFileName(zipPath),
                CurrentItemIndex = totalItems,
                TotalItems = totalItems,
                BytesProcessed = totalBytes,
                TotalBytes = totalBytes,
                IsCompleted = true
            });

            return CompressionResult.Ok(zipPath, itemsProcessed);
        }
        catch (OperationCanceledException)
        {
            // Clean up incomplete ZIP file
            CleanupIncompleteZip(zipPath);
            progress?.Report(new FileOperationProgress
            {
                OperationType = FileOperationType.Compress,
                IsCancelled = true,
                IsCompleted = true
            });
            return CompressionResult.CancelledResult();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied during compression");
            CleanupIncompleteZip(zipPath);
            return CompressionResult.Failed($"Access denied: {ex.Message}");
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "File not found during compression");
            CleanupIncompleteZip(zipPath);
            return CompressionResult.Failed($"File not found: {ex.Message}");
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Directory not found during compression");
            CleanupIncompleteZip(zipPath);
            return CompressionResult.Failed($"Directory not found: {ex.Message}");
        }
        catch (IOException ex) when (ex.Message.Contains("space", StringComparison.OrdinalIgnoreCase)
                                     || ex.HResult == unchecked((int)0x80070070)) // ERROR_DISK_FULL
        {
            _logger.LogError(ex, "Insufficient disk space during compression");
            CleanupIncompleteZip(zipPath);
            return CompressionResult.Failed("Not enough disk space to create the archive.");
        }
        catch (PathTooLongException ex)
        {
            _logger.LogError(ex, "Path too long during compression");
            CleanupIncompleteZip(zipPath);
            return CompressionResult.Failed($"Path too long: {ex.Message}");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error during compression");
            CleanupIncompleteZip(zipPath);
            return CompressionResult.Failed($"I/O error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during compression");
            CleanupIncompleteZip(zipPath);
            return CompressionResult.Failed($"Compression failed: {ex.Message}");
        }
    }

    private static string GenerateZipName(IReadOnlyList<string> sourcePaths)
    {
        if (sourcePaths.Count == 1)
        {
            // Single item: use its name without extension
            var name = Path.GetFileNameWithoutExtension(sourcePaths[0]);
            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileName(sourcePaths[0]);
            return name;
        }

        // Multiple items: use "Archive"
        return "Archive";
    }

    private static string GetUniqueZipPath(string directory, string baseName)
    {
        var zipPath = Path.Combine(directory, baseName + ".zip");
        if (!File.Exists(zipPath))
            return zipPath;

        var counter = 1;
        while (File.Exists(zipPath))
        {
            zipPath = Path.Combine(directory, $"{baseName} ({counter}).zip");
            counter++;
        }

        return zipPath;
    }

    private static List<ZipEntryInfo> CollectEntries(IReadOnlyList<string> sourcePaths, CancellationToken cancellationToken)
    {
        var entries = new List<ZipEntryInfo>();

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(sourcePath))
            {
                var fileInfo = new FileInfo(sourcePath);
                entries.Add(new ZipEntryInfo
                {
                    FullPath = sourcePath,
                    RelativePath = fileInfo.Name,
                    Size = fileInfo.Length,
                    IsDirectory = false
                });
            }
            else if (Directory.Exists(sourcePath))
            {
                var dirInfo = new DirectoryInfo(sourcePath);
                var baseName = dirInfo.Name;

                // Add directory entry
                entries.Add(new ZipEntryInfo
                {
                    FullPath = sourcePath,
                    RelativePath = baseName + "/",
                    Size = 0,
                    IsDirectory = true
                });

                // Recursively add all contents
                CollectDirectoryEntries(sourcePath, baseName, entries, cancellationToken);
            }
        }

        return entries;
    }

    private static void CollectDirectoryEntries(string directoryPath, string relativePath, List<ZipEntryInfo> entries, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Add subdirectories
        foreach (var subDir in Directory.EnumerateDirectories(directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subDirName = Path.GetFileName(subDir);
            var subRelative = relativePath + "/" + subDirName;

            entries.Add(new ZipEntryInfo
            {
                FullPath = subDir,
                RelativePath = subRelative + "/",
                Size = 0,
                IsDirectory = true
            });

            CollectDirectoryEntries(subDir, subRelative, entries, cancellationToken);
        }

        // Add files
        foreach (var file in Directory.EnumerateFiles(directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(file);
            entries.Add(new ZipEntryInfo
            {
                FullPath = file,
                RelativePath = relativePath + "/" + fileInfo.Name,
                Size = fileInfo.Length,
                IsDirectory = false
            });
        }
    }

    private static void ReportProgress(IProgress<FileOperationProgress>? progress, string currentItem,
        int itemsProcessed, int totalItems, long bytesProcessed, long totalBytes, Stopwatch stopwatch)
    {
        if (progress is null) return;

        var elapsed = stopwatch.Elapsed.TotalSeconds;
        var bytesPerSecond = elapsed > 0 ? bytesProcessed / elapsed : 0;
        TimeSpan? remaining = null;
        if (bytesPerSecond > 0 && totalBytes > bytesProcessed)
        {
            var remainingBytes = totalBytes - bytesProcessed;
            remaining = TimeSpan.FromSeconds(remainingBytes / bytesPerSecond);
        }

        progress.Report(new FileOperationProgress
        {
            OperationType = FileOperationType.Compress,
            CurrentItem = currentItem,
            CurrentItemIndex = itemsProcessed,
            TotalItems = totalItems,
            BytesProcessed = bytesProcessed,
            TotalBytes = totalBytes,
            BytesPerSecond = bytesPerSecond,
            EstimatedRemaining = remaining
        });
    }

    /// <summary>True when at least <see cref="ProgressThrottleMs"/> have passed since the last report.</summary>
    private static bool ShouldReport(Stopwatch stopwatch, ref long lastReportMs)
    {
        var now = stopwatch.ElapsedMilliseconds;
        if (lastReportMs < 0 || now - lastReportMs >= ProgressThrottleMs)
        {
            lastReportMs = now;
            return true;
        }
        return false;
    }

    private void CleanupIncompleteZip(string? zipPath)
    {
        if (zipPath is null) return;
        try
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up incomplete ZIP: {Path}", zipPath);
        }
    }

    public async Task<CompressionResult> ExtractAsync(
        string archivePath,
        string? destinationDirectory = null,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            return CompressionResult.Failed("Archive file not found.");

        try
        {
            // Determine output directory
            if (string.IsNullOrEmpty(destinationDirectory))
            {
                var parentDir = Path.GetDirectoryName(archivePath)
                    ?? throw new InvalidOperationException("Cannot determine output directory.");
                var baseName = Path.GetFileNameWithoutExtension(archivePath);
                destinationDirectory = Path.Combine(parentDir, baseName);

                // Generate unique directory name if it already exists
                if (Directory.Exists(destinationDirectory))
                {
                    var counter = 1;
                    var original = destinationDirectory;
                    while (Directory.Exists(destinationDirectory))
                    {
                        destinationDirectory = $"{original} ({counter})";
                        counter++;
                    }
                }
            }

            Directory.CreateDirectory(destinationDirectory);

            var stopwatch = Stopwatch.StartNew();
            long bytesProcessed = 0;
            var itemsProcessed = 0;
            long lastReportMs = -1;

            await Task.Run(async () =>
            {
                using var zipStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                var totalItems = archive.Entries.Count;
                var totalBytes = archive.Entries.Sum(e => e.Length);

                // Emit an initial 0% report so the UI shows the operation immediately.
                ReportExtractProgress(progress, Path.GetFileName(archivePath), 0, totalItems, 0, totalBytes, stopwatch);

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entryPath = Path.Combine(destinationDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar));

                    // Security: prevent path traversal (Zip Slip). Compare against the destination
                    // root WITH a trailing separator so a sibling like "out\data-evil" cannot pass
                    // a prefix check against "out\data".
                    var fullPath = Path.GetFullPath(entryPath);
                    var destRoot = Path.GetFullPath(destinationDirectory);
                    var destRootWithSep = destRoot.EndsWith(Path.DirectorySeparatorChar)
                        ? destRoot
                        : destRoot + Path.DirectorySeparatorChar;
                    if (!string.Equals(fullPath, destRoot, StringComparison.OrdinalIgnoreCase)
                        && !fullPath.StartsWith(destRootWithSep, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Skipping entry with path traversal: {Entry}", entry.FullName);
                        itemsProcessed++;
                        continue;
                    }

                    // Directory entry (ends with /)
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(fullPath);
                        itemsProcessed++;
                        ReportExtractProgress(progress, entry.FullName, itemsProcessed, totalItems, bytesProcessed, totalBytes, stopwatch);
                        continue;
                    }

                    // Ensure parent directory exists
                    var entryDir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(entryDir))
                        Directory.CreateDirectory(entryDir);

                    // Extract file with streaming
                    using var entryStream = entry.Open();
                    using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

                    var buffer = new byte[BufferSize];
                    int bytesRead;
                    while ((bytesRead = await entryStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        bytesProcessed += bytesRead;
                        // Throttle UI updates to keep the window responsive during large extractions.
                        if (ShouldReport(stopwatch, ref lastReportMs))
                            ReportExtractProgress(progress, entry.FullName, itemsProcessed, totalItems, bytesProcessed, totalBytes, stopwatch);
                    }

                    itemsProcessed++;
                    ReportExtractProgress(progress, entry.FullName, itemsProcessed, totalItems, bytesProcessed, totalBytes, stopwatch);
                }
            }, cancellationToken);

            // Report completion
            progress?.Report(new FileOperationProgress
            {
                OperationType = FileOperationType.Extract,
                CurrentItem = Path.GetFileName(archivePath),
                CurrentItemIndex = itemsProcessed,
                TotalItems = itemsProcessed,
                BytesProcessed = bytesProcessed,
                TotalBytes = bytesProcessed,
                IsCompleted = true
            });

            return CompressionResult.Ok(destinationDirectory, itemsProcessed);
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new FileOperationProgress
            {
                OperationType = FileOperationType.Extract,
                IsCancelled = true,
                IsCompleted = true
            });
            return CompressionResult.CancelledResult();
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(ex, "Invalid archive format: {Path}", archivePath);
            return CompressionResult.Failed($"Invalid archive format: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied during extraction");
            return CompressionResult.Failed($"Access denied: {ex.Message}");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error during extraction");
            return CompressionResult.Failed($"I/O error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during extraction");
            return CompressionResult.Failed($"Extraction failed: {ex.Message}");
        }
    }

    private static void ReportExtractProgress(IProgress<FileOperationProgress>? progress, string currentItem,
        int itemsProcessed, int totalItems, long bytesProcessed, long totalBytes, Stopwatch stopwatch)
    {
        if (progress is null) return;

        var elapsed = stopwatch.Elapsed.TotalSeconds;
        var bytesPerSecond = elapsed > 0 ? bytesProcessed / elapsed : 0;
        TimeSpan? remaining = null;
        if (bytesPerSecond > 0 && totalBytes > bytesProcessed)
        {
            var remainingBytes = totalBytes - bytesProcessed;
            remaining = TimeSpan.FromSeconds(remainingBytes / bytesPerSecond);
        }

        progress.Report(new FileOperationProgress
        {
            OperationType = FileOperationType.Extract,
            CurrentItem = currentItem,
            CurrentItemIndex = itemsProcessed,
            TotalItems = totalItems,
            BytesProcessed = bytesProcessed,
            TotalBytes = totalBytes,
            BytesPerSecond = bytesPerSecond,
            EstimatedRemaining = remaining
        });
    }

    private sealed class ZipEntryInfo
    {
        public required string FullPath { get; init; }
        public required string RelativePath { get; init; }
        public long Size { get; init; }
        public bool IsDirectory { get; init; }
    }
}
