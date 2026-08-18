namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service that watches a directory for file system changes and notifies consumers.
/// Used for auto-refreshing the file explorer when external changes occur.
/// </summary>
public interface IFileWatcherService : IDisposable
{
    /// <summary>
    /// Starts watching the specified directory for changes.
    /// Any previously watched directory is automatically stopped.
    /// </summary>
    void Watch(string path);

    /// <summary>
    /// Stops watching the current directory.
    /// </summary>
    void StopWatching();

    /// <summary>
    /// Gets the currently watched path, or null if not watching.
    /// </summary>
    string? CurrentPath { get; }

    /// <summary>
    /// Fired when changes are detected in the watched directory.
    /// Events are debounced to avoid rapid-fire refreshes.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Temporarily suppresses change notifications (e.g., during our own file operations).
    /// Returns an IDisposable that resumes notifications when disposed.
    /// </summary>
    IDisposable Suppress();
}
