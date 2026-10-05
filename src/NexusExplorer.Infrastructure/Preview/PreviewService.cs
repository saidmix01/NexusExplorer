using Microsoft.Extensions.Logging;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Infrastructure.Preview;

/// <summary>
/// Implements file preview by detecting type from extension and reading content.
/// </summary>
public sealed class PreviewService : IPreviewService
{
    private readonly ILogger<PreviewService> _logger;

    /// <summary>
    /// Maximum text file size to preview (2 MB).
    /// </summary>
    internal const long MaxTextPreviewBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".log", ".csv",
        ".json", ".xml", ".yaml", ".yml",
        ".cs", ".js", ".ts", ".jsx", ".tsx",
        ".html", ".htm", ".css", ".scss",
        ".sql",
        ".rs", ".py", ".java",
        ".c", ".h", ".cpp", ".hpp", ".cc", ".cxx",
        ".sh", ".bash", ".ps1", ".bat", ".cmd",
        ".toml", ".ini", ".cfg", ".conf",
        ".env", ".gitignore", ".editorconfig",
        ".sln", ".csproj", ".fsproj", ".vbproj"
    };

    public PreviewService(ILogger<PreviewService> logger)
    {
        _logger = logger;
    }

    public bool CanPreview(FileSystemItem item)
    {
        if (item.Type == FileSystemItemType.Directory)
            return true;

        if (item.Type != FileSystemItemType.File)
            return false;

        var ext = GetExtension(item);
        return ImageExtensions.Contains(ext) || TextExtensions.Contains(ext);
    }

    public PreviewType GetPreviewType(FileSystemItem item)
    {
        if (item.Type == FileSystemItemType.Directory)
            return PreviewType.Folder;

        if (item.Type != FileSystemItemType.File)
            return PreviewType.None;

        var ext = GetExtension(item);

        if (ImageExtensions.Contains(ext))
            return PreviewType.Image;

        if (TextExtensions.Contains(ext))
            return PreviewType.Text;

        // Unknown extension: still try a text preview if the file looks like plain text.
        // This covers extensionless files and less common text formats.
        if (LooksLikeTextFile(item.Path))
            return PreviewType.Text;

        return PreviewType.Unsupported;
    }

    public async Task<PreviewResult> GetPreviewAsync(FileSystemItem item, CancellationToken cancellationToken = default)
    {
        var previewType = GetPreviewType(item);

        var metadata = new
        {
            item.Name,
            FileType = GetFileTypeDescription(item),
            item.Size,
            item.LastModified,
            item.Path
        };

        try
        {
            return previewType switch
            {
                PreviewType.Image => await GetImagePreviewAsync(item, cancellationToken),
                PreviewType.Text => await GetTextPreviewAsync(item, cancellationToken),
                PreviewType.Folder => await GetFolderPreviewAsync(item, cancellationToken),
                PreviewType.Unsupported => PreviewResult.UnsupportedFile(item),
                _ => PreviewResult.NoSelection()
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating preview for: {Path}", item.Path);
            return PreviewResult.Error($"Cannot preview file: {ex.Message}", item);
        }
    }

    private async Task<PreviewResult> GetImagePreviewAsync(FileSystemItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // For images, we pass the path and let the UI handle rendering.
        // Read dimensions straight from the file header (cheap, no full decode).
        var (width, height) = await Task.Run(
            () => ImageDimensionReader.TryRead(item.Path), cancellationToken);

        var metadata = new List<PreviewMetadataEntry>();
        if (width is int w && height is int h)
        {
            metadata.Add(new("Dimensions", $"{w} × {h} px"));
            var megaPixels = w * (double)h / 1_000_000d;
            if (megaPixels >= 0.1)
                metadata.Add(new("Resolution", $"{megaPixels:0.#} MP"));
            metadata.Add(new("Aspect ratio", FormatAspectRatio(w, h)));
        }

        return new PreviewResult
        {
            Type = PreviewType.Image,
            ImagePath = item.Path,
            FileName = item.Name,
            FileType = GetFileTypeDescription(item),
            FileSize = item.Size,
            LastModified = item.LastModified,
            Created = item.Created,
            FullPath = item.Path,
            ImageWidth = width,
            ImageHeight = height,
            Metadata = metadata
        };
    }

    private async Task<PreviewResult> GetTextPreviewAsync(FileSystemItem item, CancellationToken cancellationToken)
    {
        // Resolve the real size on disk. item.Size may be null/stale; relying on it directly
        // means a null size skips the guard and lets ReadToEnd load an arbitrarily large file
        // into memory (potential OOM / UI freeze).
        long? effectiveSize = item.Size;
        try
        {
            effectiveSize = new FileInfo(item.Path).Length;
        }
        catch
        {
            // Fall back to the reported size if the file can't be stat'd right now.
        }

        // Don't load files larger than the limit (treat an unknown size as too large to be safe).
        if (effectiveSize is null || effectiveSize > MaxTextPreviewBytes)
        {
            return new PreviewResult
            {
                Type = PreviewType.Text,
                TextContent = $"[File too large to preview: {FormatSize(effectiveSize ?? 0)}. Maximum preview size is {FormatSize(MaxTextPreviewBytes)}.]",
                FileName = item.Name,
                FileType = GetFileTypeDescription(item),
                FileSize = effectiveSize,
                LastModified = item.LastModified,
                FullPath = item.Path
            };
        }

        var content = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var reader = new StreamReader(item.Path, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }, cancellationToken);

        var lineCount = CountLines(content);
        var metadata = new List<PreviewMetadataEntry>
        {
            new("Lines", lineCount.ToString("N0")),
            new("Characters", content.Length.ToString("N0"))
        };

        return new PreviewResult
        {
            Type = PreviewType.Text,
            TextContent = content,
            FileName = item.Name,
            FileType = GetFileTypeDescription(item),
            FileSize = item.Size,
            LastModified = item.LastModified,
            Created = item.Created,
            FullPath = item.Path,
            Metadata = metadata
        };
    }

    /// <summary>Maximum number of entries listed in a folder preview.</summary>
    private const int MaxFolderEntries = 500;

    private async Task<PreviewResult> GetFolderPreviewAsync(FileSystemItem item, CancellationToken cancellationToken)
    {
        var (text, folderCount, fileCount) = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directories = new List<string>();
            var files = new List<string>();

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(item.Path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    directories.Add(System.IO.Path.GetFileName(dir));
                }

                foreach (var file in Directory.EnumerateFiles(item.Path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    files.Add(System.IO.Path.GetFileName(file));
                }
            }
            catch (UnauthorizedAccessException)
            {
                return ("[Access denied.]", 0, 0);
            }
            catch (DirectoryNotFoundException)
            {
                return ("[Folder not found.]", 0, 0);
            }

            directories.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);

            var totalFolders = directories.Count;
            var totalFiles = files.Count;

            var sb = new System.Text.StringBuilder();
            var listed = 0;

            // Folders first (prefixed so they read as folders), then files.
            foreach (var d in directories)
            {
                if (listed >= MaxFolderEntries) break;
                sb.Append('\uD83D').Append('\uDCC1').Append("  ").AppendLine(d); // 📁
                listed++;
            }

            foreach (var f in files)
            {
                if (listed >= MaxFolderEntries) break;
                sb.Append('\uD83D').Append('\uDCC4').Append("  ").AppendLine(f); // 📄
                listed++;
            }

            if (totalFolders + totalFiles == 0)
                sb.AppendLine("[Empty folder]");
            else if (totalFolders + totalFiles > MaxFolderEntries)
                sb.AppendLine().Append("… and ")
                  .Append(totalFolders + totalFiles - MaxFolderEntries)
                  .AppendLine(" more item(s) not shown.");

            return (sb.ToString().TrimEnd(), totalFolders, totalFiles);
        }, cancellationToken);

        var summary = FormatItemCounts(fileCount, folderCount);

        var metadata = new List<PreviewMetadataEntry>
        {
            new("Folders", folderCount.ToString("N0")),
            new("Files", fileCount.ToString("N0")),
            new("Items", (folderCount + fileCount).ToString("N0"))
        };

        return new PreviewResult
        {
            Type = PreviewType.Folder,
            TextContent = text,
            FileName = item.Name,
            FileType = summary,
            FileSize = null,
            LastModified = item.LastModified,
            Created = item.Created,
            FullPath = item.Path,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Heuristically detects whether a file is plain text by sampling the first few KB:
    /// a NUL byte or a high ratio of non-printable bytes indicates binary content.
    /// </summary>
    private static bool LooksLikeTextFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length == 0) return true;

            Span<byte> buffer = stackalloc byte[4096];
            var read = stream.Read(buffer);
            if (read == 0) return true;

            var sample = buffer[..read];
            var suspicious = 0;
            foreach (var b in sample)
            {
                if (b == 0) return false; // NUL byte → binary
                // Count control chars that aren't common whitespace (tab, LF, CR, FF).
                if (b < 0x09 || (b > 0x0D && b < 0x20))
                    suspicious++;
            }

            // Allow a small fraction of control bytes before calling it binary.
            return suspicious <= read / 32;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatItemCounts(int files, int folders)
    {
        var parts = new List<string>();
        parts.Add($"{folders} folder{(folders != 1 ? "s" : "")}");
        parts.Add($"{files} file{(files != 1 ? "s" : "")}");
        return string.Join(", ", parts);
    }

    private static string GetExtension(FileSystemItem item)
    {
        return item.Extension ?? System.IO.Path.GetExtension(item.Name) ?? string.Empty;
    }

    private static string GetFileTypeDescription(FileSystemItem item)
    {
        var ext = GetExtension(item).TrimStart('.').ToUpperInvariant();
        return string.IsNullOrEmpty(ext) ? "File" : $"{ext} File";
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:0.#} {units[unitIndex]}";
    }

    private static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;
        var lines = 1;
        foreach (var c in content)
            if (c == '\n') lines++;
        return lines;
    }

    /// <summary>Reduces a width×height pair to a readable aspect ratio (e.g. "16 : 9").</summary>
    private static string FormatAspectRatio(int width, int height)
    {
        if (width <= 0 || height <= 0) return "—";
        var gcd = Gcd(width, height);
        return $"{width / gcd} : {height / gcd}";
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }
        return a == 0 ? 1 : a;
    }
}
