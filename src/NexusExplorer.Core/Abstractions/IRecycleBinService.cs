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