using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for discovering and executing "Send To" targets.
/// Platform-specific implementations resolve actual system destinations.
/// </summary>
public interface ISendToService
{
    /// <summary>
    /// Gets the available Send To targets for the current platform.
    /// </summary>
    IReadOnlyList<SendToTarget> GetTargets();

    /// <summary>
    /// Sends the specified files/folders to the given target.
    /// </summary>
    /// <param name="sourcePaths">Files and/or directories to send.</param>
    /// <param name="target">The destination target.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="conflictResolver">Optional conflict resolver for name collisions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<FileOperationResult> SendToAsync(
        IReadOnlyList<string> sourcePaths,
        SendToTarget target,
        IProgress<FileOperationProgress>? progress = null,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null,
        CancellationToken cancellationToken = default);
}
