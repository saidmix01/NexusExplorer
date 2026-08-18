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
        if (item.Type != FileSystemItemType.File)
            return false;

        var ext = GetExtension(item);
        return ImageExtensions.Contains(ext) || TextExtensions.Contains(ext);
    }

    public PreviewType GetPreviewType(FileSystemItem item)
    {
        if (item.Type != FileSystemItemType.File)
            return PreviewType.None;

        var ext = GetExtension(item);

        if (ImageExtensions.Contains(ext))
            return PreviewType.Image;

        if (TextExtensions.Contains(ext))
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

    private Task<PreviewResult> GetImagePreviewAsync(FileSystemItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // For images, we pass the path and let the UI handle rendering.
        // Try to read dimensions from the file header for metadata.
        var result = new PreviewResult
        {
            Type = PreviewType.Image,
            ImagePath = item.Path,
            FileName = item.Name,
            FileType = GetFileTypeDescription(item),
            FileSize = item.Size,
            LastModified = item.LastModified,
            FullPath = item.Path
        };

        return Task.FromResult(result);
    }

    private async Task<PreviewResult> GetTextPreviewAsync(FileSystemItem item, CancellationToken cancellationToken)
    {
        // Don't load files larger than the limit
        if (item.Size > MaxTextPreviewBytes)
        {
            return new PreviewResult
            {
                Type = PreviewType.Text,
                TextContent = $"[File too large to preview: {FormatSize(item.Size ?? 0)}. Maximum preview size is {FormatSize(MaxTextPreviewBytes)}.]",
                FileName = item.Name,
                FileType = GetFileTypeDescription(item),
                FileSize = item.Size,
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

        return new PreviewResult
        {
            Type = PreviewType.Text,
            TextContent = content,
            FileName = item.Name,
            FileType = GetFileTypeDescription(item),
            FileSize = item.Size,
            LastModified = item.LastModified,
            FullPath = item.Path
        };
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
}
