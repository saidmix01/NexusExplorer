using Microsoft.Extensions.Logging;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Infrastructure.FileOperations;

/// <summary>
/// Implements undo history for file operations.
/// Maintains a bounded stack of completed operations.
/// </summary>
public sealed class OperationHistoryService : IOperationHistoryService
{
    private readonly Stack<UndoableOperation> _history = new();
    private readonly Stack<UndoableOperation> _redoStack = new();
    private readonly ILogger<OperationHistoryService> _logger;
    private const int MaxHistorySize = 100;

    public event EventHandler? HistoryChanged;

    public OperationHistoryService(ILogger<OperationHistoryService> logger)
    {
        _logger = logger;
    }

    public bool CanUndo => _history.Count > 0 && _history.Peek().IsReversible;
    public bool CanRedo => _redoStack.Count > 0 && _redoStack.Peek().IsReversible;

    public string? UndoDescription
    {
        get
        {
            if (_history.Count == 0) return null;
            var op = _history.Peek();
            return op.IsReversible ? $"Undo {op.Description}" : null;
        }
    }

    public string? RedoDescription
    {
        get
        {
            if (_redoStack.Count == 0) return null;
            var op = _redoStack.Peek();
            return op.IsReversible ? $"Redo {op.Description}" : null;
        }
    }

    public void AddOperation(UndoableOperation operation)
    {
        _history.Push(operation);
        _redoStack.Clear(); // New operation invalidates redo stack

        // Trim history if it exceeds the limit
        if (_history.Count > MaxHistorySize)
        {
            var temp = _history.ToArray();
            _history.Clear();
            for (int i = Math.Min(temp.Length - 1, MaxHistorySize - 1); i >= 0; i--)
                _history.Push(temp[i]);
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<UndoResult> UndoAsync(CancellationToken cancellationToken = default)
    {
        if (_history.Count == 0)
            return UndoResult.NoOperation();

        var operation = _history.Peek();

        if (!operation.IsReversible)
            return UndoResult.Failed("This operation cannot be undone.");

        try
        {
            var result = await ExecuteUndoAsync(operation, cancellationToken);

            if (result.Success)
            {
                _history.Pop();
                _redoStack.Push(operation); // Push to redo stack
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return UndoResult.Failed("Undo was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Undo failed for operation: {Type}", operation.Type);
            return UndoResult.Failed($"Undo failed: {ex.Message}");
        }
    }

    public async Task<UndoResult> RedoAsync(CancellationToken cancellationToken = default)
    {
        if (_redoStack.Count == 0)
            return UndoResult.NoOperation();

        var operation = _redoStack.Peek();

        if (!operation.IsReversible)
            return UndoResult.Failed("This operation cannot be redone.");

        try
        {
            var result = await ExecuteRedoAsync(operation, cancellationToken);

            if (result.Success)
            {
                _redoStack.Pop();
                _history.Push(operation); // Push back to undo stack
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return UndoResult.Failed("Redo was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redo failed for operation: {Type}", operation.Type);
            return UndoResult.Failed($"Redo failed: {ex.Message}");
        }
    }

    public void Clear()
    {
        _history.Clear();
        _redoStack.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<UndoResult> ExecuteUndoAsync(UndoableOperation operation, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            int reverted = 0;
            var errors = new List<string>();

            foreach (var entry in operation.Entries)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    switch (operation.Type)
                    {
                        case UndoOperationType.Copy:
                            UndoCopy(entry);
                            break;
                        case UndoOperationType.Move:
                            UndoMove(entry);
                            break;
                        case UndoOperationType.Rename:
                            UndoRename(entry);
                            break;
                        case UndoOperationType.CreateFile:
                        case UndoOperationType.CreateFolder:
                            UndoCreate(entry);
                            break;
                        case UndoOperationType.Delete:
                            // Delete undo is not supported (cannot restore from recycle bin programmatically)
                            errors.Add($"Cannot restore '{Path.GetFileName(entry.SourcePath)}'.");
                            continue;
                    }
                    reverted++;
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Undo failed for entry: {Path}", entry.DestinationPath);
                    errors.Add($"'{Path.GetFileName(entry.DestinationPath)}': {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Access denied during undo: {Path}", entry.DestinationPath);
                    errors.Add($"'{Path.GetFileName(entry.DestinationPath)}': Access denied.");
                }
            }

            if (errors.Count > 0 && reverted == 0)
                return UndoResult.Failed(string.Join("\n", errors));

            return UndoResult.Ok(reverted);
        }, ct);
    }

    /// <summary>
    /// Undo Copy: delete the copied file/folder (the destination).
    /// </summary>
    private void UndoCopy(UndoEntry entry)
    {
        var path = entry.DestinationPath;

        if (entry.IsDirectory)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        else
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// Undo Move: move the item back from destination to source.
    /// </summary>
    private void UndoMove(UndoEntry entry)
    {
        var currentPath = entry.DestinationPath;
        var originalPath = entry.SourcePath;

        // Check that current location still exists
        if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
            throw new FileNotFoundException($"Cannot find '{Path.GetFileName(currentPath)}' to move back.");

        // Check that target doesn't already exist (conflict)
        if (File.Exists(originalPath) || Directory.Exists(originalPath))
            throw new IOException($"'{Path.GetFileName(originalPath)}' already exists at the original location.");

        // Ensure parent directory exists
        var parentDir = Path.GetDirectoryName(originalPath);
        if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            Directory.CreateDirectory(parentDir);

        if (entry.IsDirectory)
            Directory.Move(currentPath, originalPath);
        else
            File.Move(currentPath, originalPath);
    }

    /// <summary>
    /// Undo Rename: rename back from new name to old name.
    /// </summary>
    private void UndoRename(UndoEntry entry)
    {
        var currentPath = entry.DestinationPath; // The new name
        var originalPath = entry.SourcePath;     // The old name

        if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
            throw new FileNotFoundException($"Cannot find '{Path.GetFileName(currentPath)}' to rename back.");

        if (File.Exists(originalPath) || Directory.Exists(originalPath))
            throw new IOException($"'{Path.GetFileName(originalPath)}' already exists.");

        if (entry.IsDirectory)
            Directory.Move(currentPath, originalPath);
        else
            File.Move(currentPath, originalPath);
    }

    /// <summary>
    /// Undo Create: delete the created file or empty folder.
    /// </summary>
    private static void UndoCreate(UndoEntry entry)
    {
        var path = entry.DestinationPath;

        if (entry.IsDirectory)
        {
            if (!Directory.Exists(path)) return;

            // Safety: only delete if the folder is empty or still has no user content
            var dirInfo = new DirectoryInfo(path);
            if (dirInfo.EnumerateFileSystemInfos().Any())
                throw new IOException($"Folder '{dirInfo.Name}' is not empty. Cannot undo creation safely.");

            Directory.Delete(path, false);
        }
        else
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// Re-executes an undone operation (redo). Reverses the undo for each operation type.
    /// </summary>
    private async Task<UndoResult> ExecuteRedoAsync(UndoableOperation operation, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            int redone = 0;
            var errors = new List<string>();

            foreach (var entry in operation.Entries)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    switch (operation.Type)
                    {
                        case UndoOperationType.Copy:
                            // Redo copy: copy the source back to destination
                            RedoCopy(entry);
                            break;
                        case UndoOperationType.Move:
                            // Redo move: move from source to destination again
                            RedoMove(entry);
                            break;
                        case UndoOperationType.Rename:
                            // Redo rename: rename from source to destination again
                            RedoRename(entry);
                            break;
                        case UndoOperationType.CreateFile:
                            // Redo create file
                            if (!File.Exists(entry.DestinationPath))
                                File.Create(entry.DestinationPath).Dispose();
                            break;
                        case UndoOperationType.CreateFolder:
                            // Redo create folder
                            if (!Directory.Exists(entry.DestinationPath))
                                Directory.CreateDirectory(entry.DestinationPath);
                            break;
                        case UndoOperationType.Delete:
                            errors.Add($"Cannot redo delete for '{Path.GetFileName(entry.SourcePath)}'.");
                            continue;
                    }
                    redone++;
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Redo failed for entry: {Path}", entry.DestinationPath);
                    errors.Add($"'{Path.GetFileName(entry.DestinationPath)}': {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Access denied during redo: {Path}", entry.DestinationPath);
                    errors.Add($"'{Path.GetFileName(entry.DestinationPath)}': Access denied.");
                }
            }

            if (errors.Count > 0 && redone == 0)
                return UndoResult.Failed(string.Join("\n", errors));

            return UndoResult.Ok(redone);
        }, ct);
    }

    private static void RedoCopy(UndoEntry entry)
    {
        if (entry.IsDirectory)
        {
            // Re-copy directory from source to destination
            if (Directory.Exists(entry.SourcePath) && !Directory.Exists(entry.DestinationPath))
                CopyDirectory(entry.SourcePath, entry.DestinationPath);
        }
        else
        {
            if (File.Exists(entry.SourcePath) && !File.Exists(entry.DestinationPath))
                File.Copy(entry.SourcePath, entry.DestinationPath);
        }
    }

    private static void RedoMove(UndoEntry entry)
    {
        // After undo, item is back at SourcePath. Redo moves it to DestinationPath again.
        if (entry.IsDirectory)
        {
            if (Directory.Exists(entry.SourcePath))
                Directory.Move(entry.SourcePath, entry.DestinationPath);
        }
        else
        {
            if (File.Exists(entry.SourcePath))
                File.Move(entry.SourcePath, entry.DestinationPath);
        }
    }

    private static void RedoRename(UndoEntry entry)
    {
        // After undo, item is back at SourcePath. Redo renames it to DestinationPath again.
        if (entry.IsDirectory)
        {
            if (Directory.Exists(entry.SourcePath))
                Directory.Move(entry.SourcePath, entry.DestinationPath);
        }
        else
        {
            if (File.Exists(entry.SourcePath))
                File.Move(entry.SourcePath, entry.DestinationPath);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
}
