using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Abstraction for file preview functionality.
/// </summary>
public interface IPreviewService
{
    /// <summary>
    /// Determines whether a file can be previewed.
    /// </summary>
    bool CanPreview(FileSystemItem item);

    /// <summary>
    /// Gets the preview type for a file based on its extension.
    /// </summary>
    PreviewType GetPreviewType(FileSystemItem item);

    /// <summary>
    /// Generates a full preview result for the given file.
    /// </summary>
    Task<PreviewResult> GetPreviewAsync(FileSystemItem item, CancellationToken cancellationToken = default);
}
