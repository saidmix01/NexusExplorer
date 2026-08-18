namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for tracking file operations and undoing them.
/// Global to the application (not per-tab).
/// </summary>
public interface IOperationHistoryService
{
    /// <summary>
    /// Whether there is an operation that can be undone.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Whether there is an operation that can be redone.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Human-readable description of the last undoable operation.
    /// </summary>
    string? UndoDescription { get; }

    /// <summary>
    /// Human-readable description of the last redoable operation.
    /// </summary>
    string? RedoDescription { get; }

    /// <summary>
    /// Records a completed operation in the history.
    /// </summary>
    void AddOperation(UndoableOperation operation);

    /// <summary>
    /// Undoes the last operation in the history.
    /// Returns a result describing what happened.
    /// </summary>
    Task<UndoResult> UndoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Redoes the last undone operation.
    /// Returns a result describing what happened.
    /// </summary>
    Task<UndoResult> RedoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all history.
    /// </summary>
    void Clear();

    /// <summary>
    /// Fired when CanUndo or UndoDescription changes.
    /// </summary>
    event EventHandler? HistoryChanged;
}

/// <summary>
/// The type of file operation that was performed.
/// </summary>
public enum UndoOperationType
{
    Copy,
    Move,
    Rename,
    Delete,
    CreateFile,
    CreateFolder
}

/// <summary>
/// Describes a single file operation entry (which may involve multiple items).
/// </summary>
public sealed class UndoableOperation
{
    public required UndoOperationType Type { get; init; }

    /// <summary>
    /// Human-readable short description, e.g. "Copy 'file.txt'" or "Move 5 items".
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Individual item entries for undoing.
    /// </summary>
    public required IReadOnlyList<UndoEntry> Entries { get; init; }

    /// <summary>
    /// Whether this operation can actually be undone.
    /// Permanent deletes and non-restorable operations set this to false.
    /// </summary>
    public bool IsReversible { get; init; } = true;
}

/// <summary>
/// Describes a single item within an undoable operation.
/// </summary>
public sealed class UndoEntry
{
    /// <summary>
    /// The source/original path of the item.
    /// For Copy: the destination (created file) to delete.
    /// For Move: the original path to restore to.
    /// For Rename: the old path.
    /// For Create: the created path to delete.
    /// For Delete: the original path (for potential restore).
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// The destination/current path of the item after the operation.
    /// For Copy: the created file path.
    /// For Move: the new location.
    /// For Rename: the new path.
    /// For Create: same as SourcePath.
    /// For Delete: empty (item no longer exists).
    /// </summary>
    public required string DestinationPath { get; init; }

    /// <summary>
    /// Whether this entry is a directory.
    /// </summary>
    public bool IsDirectory { get; init; }
}

/// <summary>
/// Result of an undo operation.
/// </summary>
public sealed class UndoResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int ItemsReverted { get; init; }

    public static UndoResult Ok(int items = 1) => new() { Success = true, ItemsReverted = items };
    public static UndoResult Failed(string error) => new() { Success = false, Error = error };
    public static UndoResult NoOperation() => new() { Success = false, Error = "Nothing to undo." };
}
