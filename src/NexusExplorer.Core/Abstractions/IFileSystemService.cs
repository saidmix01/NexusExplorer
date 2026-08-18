using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Abstraction for file system operations.
/// </summary>
public interface IFileSystemService
{
    Task<IReadOnlyList<FileSystemItem>> GetItemsAsync(string path, CancellationToken cancellationToken = default);
    Task<FileSystemItem?> GetItemAsync(string path, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NavigationItem>> GetDrivesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed drive information including space usage for This PC view.
    /// </summary>
    Task<IReadOnlyList<DriveItem>> GetDriveItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of items (files + folders) directly inside a directory.
    /// Returns null if the count cannot be obtained (permission denied, etc.)
    /// </summary>
    Task<int?> GetDirectoryItemCountAsync(string path, CancellationToken cancellationToken = default);
}
