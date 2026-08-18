using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for saving and restoring application state across sessions.
/// </summary>
public interface IStatePersistenceService
{
    /// <summary>
    /// Saves the current application state to persistent storage.
    /// </summary>
    Task SaveAsync(AppState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the last saved application state, or null if none exists.
    /// </summary>
    Task<AppState?> LoadAsync(CancellationToken cancellationToken = default);
}
