using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for performing file system operations (copy, move, delete, rename, create).
/// All operations are asynchronous, support cancellation, and report progress.
/// </summary>
public interface IFileOperationService
{
    /// <summary>
    /// Copies files/directories to a destination folder.
    /// </summary>
    Task<FileOperationResult> CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileOperationProgress>? progress = null,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves files/directories to a destination folder.
    /// </summary>
    Task<FileOperationResult> MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileOperationProgress>? progress = null,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes files/directories permanently.
    /// </summary>
    Task<FileOperationResult> DeleteAsync(
        IReadOnlyList<string> paths,
        bool useRecycleBin = true,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a single file or directory.
    /// </summary>
    Task<FileOperationResult> RenameAsync(
        string path,
        string newName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new directory. Resolves name conflicts automatically (e.g., "New Folder (2)").
    /// Returns the path of the created directory.
    /// </summary>
    Task<(FileOperationResult Result, string? CreatedPath)> CreateDirectoryAsync(
        string parentDirectory,
        string? suggestedName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an empty file. When <paramref name="autoResolveConflicts"/> is true, a name clash is
    /// resolved automatically (e.g. "New Text Document (2).txt") instead of failing — matching the
    /// behavior of <see cref="CreateDirectoryAsync"/>. Returns the path of the created file.
    /// </summary>
    Task<(FileOperationResult Result, string? CreatedPath)> CreateFileAsync(
        string parentDirectory,
        string fileName,
        bool autoResolveConflicts = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new file in <paramref name="parentDirectory"/> described by a shell "New" entry.
    /// The creation mechanism is chosen from <see cref="NewItemDefinition.Kind"/>: an empty file, a
    /// copy of a template document, or inline data. Folder definitions are handled by the caller via
    /// <see cref="CreateDirectoryAsync"/>. Names are de-duplicated automatically so the action never
    /// fails on an existing name. Returns the path of the created file.
    /// </summary>
    Task<(FileOperationResult Result, string? CreatedPath)> CreateFromDefinitionAsync(
        string parentDirectory,
        NewItemDefinition definition,
        CancellationToken cancellationToken = default);
}
