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
    /// Creates an empty file.
    /// </summary>
    Task<(FileOperationResult Result, string? CreatedPath)> CreateFileAsync(
        string parentDirectory,
        string fileName,
        CancellationToken cancellationToken = default);
}
