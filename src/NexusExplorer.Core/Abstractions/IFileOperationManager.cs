using System.Collections.ObjectModel;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Maintains the state of all active file operations independently of any UI.
/// The UI observes <see cref="Operations"/> and <see cref="Changed"/> to render
/// the File Operation Center, while the operations themselves keep running in
/// the background when the panel is minimized.
/// </summary>
public interface IFileOperationManager
{
    /// <summary>
    /// All tracked operations (running, pending and recently finished).
    /// </summary>
    ObservableCollection<FileOperation> Operations { get; }

    /// <summary>
    /// True while at least one operation is pending or running.
    /// </summary>
    bool HasActiveOperations { get; }

    /// <summary>
    /// Raised whenever an operation starts, progresses, changes state or is removed.
    /// </summary>
    event EventHandler? Changed;

    Task<FileOperationResult> CopyAsync(
        IReadOnlyList<string> sources,
        string destination,
        string title,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null);

    Task<FileOperationResult> MoveAsync(
        IReadOnlyList<string> sources,
        string destination,
        string title,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null);

    Task<FileOperationResult> DeleteAsync(
        IReadOnlyList<string> paths,
        bool useRecycleBin,
        string title);

    void CancelOperation(FileOperation operation);

    void CancelAll();

    Task CancelAllAndWaitAsync();
}
