using System.Collections.Generic;

namespace NexusExplorer.Core.Models;

/// <summary>
/// A single label/value metadata row shown in the preview panel (e.g. "Dimensions" → "1920 × 1080").
/// </summary>
public sealed class PreviewMetadataEntry
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    public PreviewMetadataEntry() { }

    public PreviewMetadataEntry(string label, string value)
    {
        Label = label;
        Value = value;
    }
}

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
    public DateTime? Created { get; init; }
    public string? FullPath { get; init; }

    // Image-specific
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }

    /// <summary>
    /// Rich label/value rows (dimensions, item counts, line count, etc.) shown
    /// as a details list in the preview panel. Populated per preview type.
    /// </summary>
    public IReadOnlyList<PreviewMetadataEntry> Metadata { get; init; } = [];

    public static PreviewResult NoSelection() => new() { Type = PreviewType.None };

    public static PreviewResult UnsupportedFile(FileSystemItem item) => new()
    {
        Type = PreviewType.Unsupported,
        FileName = item.Name,
        FileType = item.Extension ?? "Unknown",
        FileSize = item.Size,
        LastModified = item.LastModified,
        Created = item.Created,
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
        Created = item.Created,
        FullPath = item.Path
    };
}
