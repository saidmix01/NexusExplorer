using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for retrieving detailed file/folder properties and metadata.
/// </summary>
public interface IFilePropertiesService
{
    /// <summary>
    /// Gets detailed properties for a single file or directory.
    /// </summary>
    Task<FileProperties> GetPropertiesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the total size of a directory recursively.
    /// Reports progress via the callback.
    /// </summary>
    Task<DirectorySizeResult> CalculateDirectorySizeAsync(string path, IProgress<DirectorySizeProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets file attributes (Hidden, ReadOnly) on the given path.
    /// </summary>
    Task SetAttributesAsync(string path, bool isReadOnly, bool isHidden, CancellationToken cancellationToken = default);
}

/// <summary>
/// Detailed properties of a file or directory.
/// </summary>
public sealed class FileProperties
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string DirectoryPath { get; init; }
    public required bool IsDirectory { get; init; }
    public required string TypeDescription { get; init; }
    public long Size { get; init; }
    public DateTime? Created { get; init; }
    public DateTime? Modified { get; init; }
    public DateTime? LastAccessed { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsHidden { get; init; }
    public int FileCount { get; init; }
    public int FolderCount { get; init; }
}

/// <summary>
/// Result of a directory size calculation.
/// </summary>
public sealed class DirectorySizeResult
{
    public long TotalSize { get; init; }
    public int FileCount { get; init; }
    public int FolderCount { get; init; }
    public bool WasCompleted { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Progress data for folder size calculation.
/// </summary>
public sealed class DirectorySizeProgress
{
    public long CurrentSize { get; init; }
    public int FileCount { get; init; }
    public int FolderCount { get; init; }
}
