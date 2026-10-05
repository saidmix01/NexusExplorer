using System.Threading;
using System.Threading.Tasks;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for moving files and directories to the system's Recycle Bin / Trash.
/// </summary>
public interface IRecycleBinService
{
    /// <summary>
    /// Whether the platform supports a recycle bin natively.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Moves a file or directory to the recycle bin.
    /// </summary>
    Task<bool> RecycleAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// A single item currently held in the Recycle Bin.
/// </summary>
public sealed class RecycleBinEntry
{
    /// <summary>Display name of the deleted item (e.g. "report.pdf").</summary>
    public required string Name { get; init; }

    /// <summary>The original full path the item was deleted from.</summary>
    public required string OriginalPath { get; init; }

    /// <summary>True if the entry was a directory.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Size in bytes, when available.</summary>
    public long? Size { get; init; }

    /// <summary>When the item was moved to the Recycle Bin, when available.</summary>
    public System.DateTime? DeletedAt { get; init; }
}

/// <summary>
/// Service for inspecting and managing the contents of the system Recycle Bin / Trash:
/// enumerate items, restore them, or empty the bin. Separate from <see cref="IRecycleBinService"/>
/// (which only sends items to the bin) so platforms without query support can no-op cleanly.
/// </summary>
public interface IRecycleBinQueryService
{
    /// <summary>Whether this platform supports listing/restoring Recycle Bin contents.</summary>
    bool IsSupported { get; }

    /// <summary>Lists the items currently in the Recycle Bin.</summary>
    Task<IReadOnlyList<RecycleBinEntry>> EnumerateAsync(CancellationToken cancellationToken = default);

    /// <summary>Restores a Recycle Bin item (identified by its original path) to its original location.</summary>
    Task<bool> RestoreAsync(string originalPath, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a single Recycle Bin item (identified by its original path).</summary>
    Task<bool> DeleteAsync(string originalPath, CancellationToken cancellationToken = default);

    /// <summary>Permanently empties the entire Recycle Bin.</summary>
    Task<bool> EmptyAsync(CancellationToken cancellationToken = default);
}