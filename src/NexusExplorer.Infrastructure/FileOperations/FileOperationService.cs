using Microsoft.Extensions.Logging;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Infrastructure.FileOperations;

/// <summary>
/// Implements file operations with buffered I/O, progress reporting, and cancellation.
/// </summary>
public sealed class FileOperationService : IFileOperationService
{
    private readonly IRecycleBinService _recycleBin;
    private readonly ILogger<FileOperationService> _logger;
    private const int BufferSize = 1024 * 1024; // 1 MB buffer — faster large-file throughput

    public FileOperationService(IRecycleBinService recycleBin, ILogger<FileOperationService> logger)
    {
        _recycleBin = recycleBin;
        _logger = logger;
    }

    public async Task<FileOperationResult> CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileOperationProgress>? progress = null,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null,
        CancellationToken cancellationToken = default)
    {
        // Operations run concurrently: each works on its own paths and its own Task, so there's
        // no need to serialize them behind a global lock (that limited the app to one at a time).
        return await CopyInternalAsync(sourcePaths, destinationDirectory, progress, conflictResolver, cancellationToken);
    }

    /// <summary>
    /// Core copy implementation, shared by CopyAsync and MoveAsync's cross-volume path.
    /// </summary>
    private Task<FileOperationResult> CopyInternalAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileOperationProgress>? progress,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            var totalBytes = CalculateTotalSize(sourcePaths);
            var state = new CopyState { TotalBytes = totalBytes, TotalItems = sourcePaths.Count };

            foreach (var source in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(source);
                var dest = Path.Combine(destinationDirectory, name);

                try
                {
                    if (Directory.Exists(source))
                    {
                        var result = await CopyDirectoryAsync(source, dest, conflictResolver, progress, state, cancellationToken);
                        if (result is not null) return result;
                    }
                    else if (File.Exists(source))
                    {
                        var result = await CopyFileAsync(source, dest, conflictResolver, progress, state, cancellationToken);
                        if (result is not null) return result;
                    }
                }
                catch (OperationCanceledException)
                {
                    return FileOperationResult.CancelledResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Copy failed: {Source} -> {Dest}", source, dest);
                    return FileOperationResult.Failed($"Failed to copy '{name}': {ex.Message}");
                }

                state.ItemsProcessed++;
                ReportProgress(progress, FileOperationType.Copy, name, state.ItemsProcessed, state.TotalItems, state.BytesProcessed, state.TotalBytes, state);
            }

            ReportCompleted(progress, FileOperationType.Copy, state.ItemsProcessed);
            return FileOperationResult.Ok(state.ItemsProcessed);
        }, cancellationToken);
    }

    public async Task<FileOperationResult> MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileOperationProgress>? progress = null,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
            {
                var itemsProcessed = 0;

                foreach (var source in sourcePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(source);
                    var dest = Path.Combine(destinationDirectory, name);

                    // Holds the temporarily-renamed pre-existing destination so we can
                    // restore it if the move fails (avoids losing the old file/folder).
                    string? replacedBackup = null;

                    try
                    {
                        // Check conflict
                        if (File.Exists(dest) || Directory.Exists(dest))
                        {
                            var action = await ResolveConflictAsync(source, dest, conflictResolver);
                            switch (action)
                            {
                                case ConflictAction.Skip: continue;
                                case ConflictAction.Cancel: return FileOperationResult.CancelledResult();
                                case ConflictAction.Replace:
                                    // Rename the existing destination aside instead of deleting it
                                    // outright, so a failed move can be rolled back without data loss.
                                    replacedBackup = GetUniquePath(dest + ".nexus-replaced");
                                    if (File.Exists(dest)) File.Move(dest, replacedBackup);
                                    else if (Directory.Exists(dest)) Directory.Move(dest, replacedBackup);
                                    break;
                                case ConflictAction.RenameAutomatically:
                                    dest = GetUniquePath(dest);
                                    break;
                            }
                        }

                        if (Directory.Exists(source))
                            Directory.Move(source, dest);
                        else if (File.Exists(source))
                            File.Move(source, dest);

                        // Move succeeded — the old destination (if any) can be discarded.
                        DeleteBackup(replacedBackup);
                        replacedBackup = null;
                    }
                    catch (OperationCanceledException)
                    {
                        RestoreBackup(replacedBackup, dest);
                        return FileOperationResult.CancelledResult();
                    }
                    catch (IOException) when (IsCrossVolume(source, dest))
                    {
                        // Cross-volume move: copy then delete, reusing the shared copy core.
                        try
                        {
                            var copyResult = await CopyInternalAsync([source], destinationDirectory, progress, conflictResolver, cancellationToken);
                            if (!copyResult.Success)
                            {
                                RestoreBackup(replacedBackup, dest);
                                return copyResult;
                            }

                            if (Directory.Exists(source)) Directory.Delete(source, true);
                            else if (File.Exists(source)) File.Delete(source);

                            DeleteBackup(replacedBackup);
                            replacedBackup = null;
                        }
                        catch (Exception ex)
                        {
                            RestoreBackup(replacedBackup, dest);
                            _logger.LogError(ex, "Cross-volume move failed: {Source} -> {Dest}", source, dest);
                            return FileOperationResult.Failed($"Failed to move '{name}': {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        RestoreBackup(replacedBackup, dest);
                        _logger.LogError(ex, "Move failed: {Source} -> {Dest}", source, dest);
                        return FileOperationResult.Failed($"Failed to move '{name}': {ex.Message}");
                    }

                    itemsProcessed++;
                    ReportProgress(progress, FileOperationType.Move, name, itemsProcessed, sourcePaths.Count, 0, 0);
                }

                ReportCompleted(progress, FileOperationType.Move, itemsProcessed);
                return FileOperationResult.Ok(itemsProcessed);
            }, cancellationToken);
    }

    /// <summary>Deletes a temporary "replaced" backup after a successful move. Best effort.</summary>
    private static void DeleteBackup(string? backupPath)
    {
        if (string.IsNullOrEmpty(backupPath)) return;
        try
        {
            if (Directory.Exists(backupPath)) Directory.Delete(backupPath, true);
            else if (File.Exists(backupPath)) File.Delete(backupPath);
        }
        catch { /* best effort — the backup is orphaned but no data is lost */ }
    }

    /// <summary>Restores a temporarily-renamed destination back to its original path after a failed move.</summary>
    private static void RestoreBackup(string? backupPath, string originalDest)
    {
        if (string.IsNullOrEmpty(backupPath)) return;
        try
        {
            // Only restore if the move did not already recreate the destination.
            if (File.Exists(originalDest) || Directory.Exists(originalDest)) return;

            if (Directory.Exists(backupPath)) Directory.Move(backupPath, originalDest);
            else if (File.Exists(backupPath)) File.Move(backupPath, originalDest);
        }
        catch { /* best effort */ }
    }

    public async Task<FileOperationResult> DeleteAsync(
        IReadOnlyList<string> paths,
        bool useRecycleBin = true,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
            {
                // Phase 1: Count total items for accurate progress
                int totalItems;
                if (useRecycleBin && _recycleBin.IsSupported)
                {
                    // Recycle bin operates per top-level item
                    totalItems = paths.Count;
                }
                else
                {
                    // For permanent delete, count all files/dirs recursively
                    totalItems = 0;
                    foreach (var path in paths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        totalItems += CountItems(path);
                    }
                }

                // Phase 2: Delete with progress
                var itemsProcessed = 0;
                var errors = new List<string>();

                foreach (var path in paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(path);

                    try
                    {
                        if (useRecycleBin && _recycleBin.IsSupported)
                        {
                            ReportProgress(progress, FileOperationType.Delete, name, itemsProcessed, totalItems, 0, 0);
                            var recycled = await _recycleBin.RecycleAsync(path, cancellationToken);
                            if (!recycled)
                            {
                                errors.Add($"Failed to move '{name}' to Recycle Bin.");
                                continue;
                            }
                            itemsProcessed++;
                            ReportProgress(progress, FileOperationType.Delete, name, itemsProcessed, totalItems, 0, 0);
                        }
                        else
                        {
                            // Permanent delete with granular progress
                            if (Directory.Exists(path))
                            {
                                itemsProcessed = DeleteDirectoryRecursive(path, progress, totalItems, itemsProcessed, errors, cancellationToken);
                            }
                            else if (File.Exists(path))
                            {
                                File.Delete(path);
                                itemsProcessed++;
                                ReportProgress(progress, FileOperationType.Delete, name, itemsProcessed, totalItems, 0, 0);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        ReportProgress(progress, FileOperationType.Delete, name, itemsProcessed, totalItems, 0, 0);
                        return FileOperationResult.CancelledResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Delete failed: {Path}", path);
                        errors.Add($"Failed to delete '{name}': {ex.Message}");
                    }
                }

                ReportCompleted(progress, FileOperationType.Delete, itemsProcessed);

                if (errors.Count > 0 && itemsProcessed == 0)
                    return FileOperationResult.Failed(string.Join("\n", errors));

                return FileOperationResult.Ok(itemsProcessed);
            }, cancellationToken);
    }

    private static int CountItems(string path)
    {
        if (File.Exists(path)) return 1;
        if (!Directory.Exists(path)) return 0;

        int count = 0;
        try
        {
            var dirInfo = new DirectoryInfo(path);
            foreach (var _ in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                count++;
            foreach (var _ in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
                count++;
            count++; // The directory itself
        }
        catch { count = 1; }
        return count;
    }

    private int DeleteDirectoryRecursive(
        string path,
        IProgress<FileOperationProgress>? progress,
        int totalItems,
        int itemsProcessed,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);

            // Delete files first
            foreach (var file in dirInfo.EnumerateFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    file.Delete();
                    itemsProcessed++;
                    if (itemsProcessed % 10 == 0 || itemsProcessed == totalItems)
                        ReportProgress(progress, FileOperationType.Delete, file.Name, itemsProcessed, totalItems, 0, 0);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to delete file: {Path}", file.FullName);
                    errors.Add($"'{file.Name}': {ex.Message}");
                    itemsProcessed++;
                }
            }

            // Recurse into subdirectories
            foreach (var subDir in dirInfo.EnumerateDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();
                itemsProcessed = DeleteDirectoryRecursive(subDir.FullName, progress, totalItems, itemsProcessed, errors, cancellationToken);
            }

            // Delete the now-empty directory
            try
            {
                dirInfo.Delete(false);
                itemsProcessed++;
                ReportProgress(progress, FileOperationType.Delete, dirInfo.Name, itemsProcessed, totalItems, 0, 0);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to delete directory: {Path}", path);
                errors.Add($"'{dirInfo.Name}': {ex.Message}");
                itemsProcessed++;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during recursive delete: {Path}", path);
            errors.Add($"'{Path.GetFileName(path)}': {ex.Message}");
        }

        return itemsProcessed;
    }

    public async Task<FileOperationResult> RenameAsync(string path, string newName, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(newName))
                        return FileOperationResult.Failed("Name cannot be empty.");

                    var invalidChars = Path.GetInvalidFileNameChars();
                    if (newName.IndexOfAny(invalidChars) >= 0)
                        return FileOperationResult.Failed("Name contains invalid characters.");

                    var directory = Path.GetDirectoryName(path);
                    if (directory is null)
                        return FileOperationResult.Failed("Invalid path.");

                    var newPath = Path.Combine(directory, newName);

                    if (string.Equals(path, newPath, StringComparison.OrdinalIgnoreCase))
                        return FileOperationResult.Ok();

                    if (File.Exists(newPath) || Directory.Exists(newPath))
                        return FileOperationResult.Failed($"'{newName}' already exists.");

                    if (Directory.Exists(path))
                        Directory.Move(path, newPath);
                    else if (File.Exists(path))
                        File.Move(path, newPath);
                    else
                        return FileOperationResult.Failed("Item not found.");

                    return FileOperationResult.Ok();
                }
                catch (Exception ex)
                {
                    return FileOperationResult.Failed(ex.Message);
                }
            }, cancellationToken);
    }

    public async Task<(FileOperationResult Result, string? CreatedPath)> CreateDirectoryAsync(
        string parentDirectory, string? suggestedName = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run<(FileOperationResult, string?)>(() =>
            {
                try
                {
                    var baseName = suggestedName ?? "New Folder";
                    var name = baseName;
                    var path = Path.Combine(parentDirectory, name);
                    var counter = 2;

                    while (Directory.Exists(path) || File.Exists(path))
                    {
                        name = $"{baseName} ({counter})";
                        path = Path.Combine(parentDirectory, name);
                        counter++;
                    }

                    Directory.CreateDirectory(path);
                    return (FileOperationResult.Ok(), path);
                }
                catch (Exception ex)
                {
                    return (FileOperationResult.Failed(ex.Message), null);
                }
            }, cancellationToken);
    }

    public async Task<(FileOperationResult Result, string? CreatedPath)> CreateFileAsync(
        string parentDirectory, string fileName, CancellationToken cancellationToken = default)
    {
        return await Task.Run<(FileOperationResult, string?)>(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(fileName))
                        return (FileOperationResult.Failed("File name cannot be empty."), null);

                    var path = Path.Combine(parentDirectory, fileName);
                    if (File.Exists(path))
                        return (FileOperationResult.Failed($"'{fileName}' already exists."), null);

                    File.Create(path).Dispose();
                    return (FileOperationResult.Ok(), path);
                }
                catch (Exception ex)
                {
                    return (FileOperationResult.Failed(ex.Message), null);
                }
            }, cancellationToken);
    }

    // --- Private helpers ---

    private async Task<FileOperationResult?> CopyFileAsync(
        string source, string dest,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver,
        IProgress<FileOperationProgress>? progress,
        CopyState state,
        CancellationToken ct)
    {
        if (File.Exists(dest))
        {
            var action = await ResolveConflictAsync(source, dest, conflictResolver);
            switch (action)
            {
                case ConflictAction.Skip: return null;
                case ConflictAction.Cancel: return FileOperationResult.CancelledResult();
                case ConflictAction.Replace: File.Delete(dest); break;
                case ConflictAction.RenameAutomatically:
                    dest = GetUniquePath(dest);
                    break;
            }
        }

        try
        {
            await using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true))
            await using (var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await sourceStream.ReadAsync(buffer, ct)) > 0)
                {
                    await destStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    state.BytesProcessed += read;

                    // Throttled: report at most every ~100ms to keep the UI responsive.
                    if (state.ShouldReport())
                        ReportProgress(progress, FileOperationType.Copy, Path.GetFileName(source),
                            state.ItemsProcessed, state.TotalItems, state.BytesProcessed, state.TotalBytes, state);
                }

                // Flush the OS write cache before the stream is disposed so the file is fully
                // written to disk (prevents "incomplete" results on large files).
                await destStream.FlushAsync(ct);
            }

            // Timestamps are set after both streams are closed so the handle isn't locked.
            try
            {
                var fi = new FileInfo(source);
                File.SetCreationTime(dest, fi.CreationTime);
                File.SetLastWriteTime(dest, fi.LastWriteTime);
            }
            catch { /* timestamp preservation is best-effort */ }
        }
        catch (OperationCanceledException)
        {
            // Remove the partial destination file so a cancelled copy leaves no corrupt data.
            TryDeleteFile(dest);
            throw;
        }

        return null;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup — the file may be in use or already removed.
        }
    }

    private async Task<FileOperationResult?> CopyDirectoryAsync(
        string source, string dest,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver,
        IProgress<FileOperationProgress>? progress,
        CopyState state,
        CancellationToken ct)
    {
        if (Directory.Exists(dest))
        {
            var action = await ResolveConflictAsync(source, dest, conflictResolver);
            switch (action)
            {
                case ConflictAction.Skip: return null;
                case ConflictAction.Cancel: return FileOperationResult.CancelledResult();
                case ConflictAction.Replace: break; // merge into existing
                case ConflictAction.RenameAutomatically:
                    dest = GetUniquePath(dest);
                    break;
            }
        }

        Directory.CreateDirectory(dest);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            ct.ThrowIfCancellationRequested();
            var fileDest = Path.Combine(dest, Path.GetFileName(file));
            var result = await CopyFileAsync(file, fileDest, conflictResolver, progress, state, ct);
            if (result is not null) return result;
        }

        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            ct.ThrowIfCancellationRequested();
            var dirDest = Path.Combine(dest, Path.GetFileName(dir));
            var result = await CopyDirectoryAsync(dir, dirDest, conflictResolver, progress, state, ct);
            if (result is not null) return result;
        }

        return null;
    }

    private sealed class CopyState
    {
        public long BytesProcessed;
        public long TotalBytes;
        public int ItemsProcessed;
        public int TotalItems;
        public System.Diagnostics.Stopwatch Stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Throttling: only surface a progress report every so often so multi-GB copies don't
        // flood the UI thread with hundreds of thousands of updates (which froze the UI).
        private long _lastReportMs = -1;
        public bool ShouldReport()
        {
            var now = Stopwatch.ElapsedMilliseconds;
            if (_lastReportMs < 0 || now - _lastReportMs >= ProgressThrottleMs)
            {
                _lastReportMs = now;
                return true;
            }
            return false;
        }
    }

    private const int ProgressThrottleMs = 100;

    private static string GetUniquePath(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        
        var counter = 1;
        var newPath = path;
        
        while (File.Exists(newPath) || Directory.Exists(newPath))
        {
            newPath = Path.Combine(dir, $"{name} ({counter}){ext}");
            counter++;
        }
        
        return newPath;
    }

    private static async Task<ConflictAction> ResolveConflictAsync(
        string source, string dest, Func<FileConflict, Task<ConflictAction>>? resolver)
    {
        if (resolver is null) return ConflictAction.Replace;

        var conflict = new FileConflict
        {
            SourcePath = source,
            DestinationPath = dest,
            FileName = Path.GetFileName(dest),
            IsDirectory = Directory.Exists(source),
            SourceSize = File.Exists(source) ? new FileInfo(source).Length : null,
            DestinationSize = File.Exists(dest) ? new FileInfo(dest).Length : null,
            SourceModified = File.Exists(source) ? File.GetLastWriteTime(source) : null,
            DestinationModified = File.Exists(dest) ? File.GetLastWriteTime(dest) : null
        };

        return await resolver(conflict);
    }

    private static long CalculateTotalSize(IReadOnlyList<string> paths)
    {
        long total = 0;
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                    total += new FileInfo(path).Length;
                else if (Directory.Exists(path))
                    total += GetDirectorySize(path);
            }
            catch { /* skip inaccessible */ }
        }
        return total;
    }

    private static long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(file).Length; } catch { }
            }
        }
        catch { }
        return size;
    }

    private static bool IsCrossVolume(string source, string dest)
    {
        return Path.GetPathRoot(source) != Path.GetPathRoot(dest);
    }

    private static void ReportProgress(IProgress<FileOperationProgress>? progress, FileOperationType type,
        string? item, int currentIndex, int totalItems, long bytesProcessed, long totalBytes, CopyState? state = null)
    {
        double bytesPerSecond = 0;
        TimeSpan? estimatedRemaining = null;

        if (state != null && bytesProcessed > 0 && state.Stopwatch.Elapsed.TotalSeconds > 0)
        {
            bytesPerSecond = bytesProcessed / state.Stopwatch.Elapsed.TotalSeconds;
            if (bytesPerSecond > 0 && totalBytes > bytesProcessed)
            {
                var secondsRemaining = (totalBytes - bytesProcessed) / bytesPerSecond;
                estimatedRemaining = TimeSpan.FromSeconds(secondsRemaining);
            }
        }

        progress?.Report(new FileOperationProgress
        {
            OperationType = type,
            CurrentItem = item,
            CurrentItemIndex = currentIndex,
            TotalItems = totalItems,
            BytesProcessed = bytesProcessed,
            TotalBytes = totalBytes,
            BytesPerSecond = bytesPerSecond,
            EstimatedRemaining = estimatedRemaining
        });
    }

    private static void ReportCompleted(IProgress<FileOperationProgress>? progress, FileOperationType type, int totalItems)
    {
        progress?.Report(new FileOperationProgress
        {
            OperationType = type,
            CurrentItemIndex = totalItems,
            TotalItems = totalItems,
            IsCompleted = true
        });
    }
}
