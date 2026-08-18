namespace NexusExplorer.Core.Models;

/// <summary>
/// Holds the result of a file preview operation.
/// </summary>
public sealed class PreviewResult
{
    public required PreviewType Type { get; init; }
    public string? TextContent { get; init; }
    public string? ImagePath { get; init; }
    public string? ErrorMessage { get; init; }

    // Metadata
    public string? FileName { get; init; }
    public string? FileType { get; init; }
    public long? FileSize { get; init; }
    public DateTime? LastModified { get; init; }
    public string? FullPath { get; init; }

    // Image-specific
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }

    public static PreviewResult NoSelection() => new() { Type = PreviewType.None };

    public static PreviewResult UnsupportedFile(FileSystemItem item) => new()
    {
        Type = PreviewType.Unsupported,
        FileName = item.Name,
        FileType = item.Extension ?? "Unknown",
        FileSize = item.Size,
        LastModified = item.LastModified,
        FullPath = item.Path
    };

    public static PreviewResult Error(string message, FileSystemItem item) => new()
    {
        Type = PreviewType.None,
        ErrorMessage = message,
        FileName = item.Name,
        FileType = item.Extension ?? "Unknown",
        FileSize = item.Size,
        LastModified = item.LastModified,
        FullPath = item.Path
    };
}
