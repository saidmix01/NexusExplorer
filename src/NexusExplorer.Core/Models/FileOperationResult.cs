namespace NexusExplorer.Core.Models;

/// <summary>
/// The result of a file operation.
/// </summary>
public sealed class FileOperationResult
{
    public bool Success { get; init; }
    public bool Cancelled { get; init; }
    public string? Error { get; init; }
    public FileConflict? Conflict { get; init; }
    public int ItemsProcessed { get; init; }

    public static FileOperationResult Ok(int items = 1) => new() { Success = true, ItemsProcessed = items };
    public static FileOperationResult Failed(string error) => new() { Success = false, Error = error };
    public static FileOperationResult CancelledResult() => new() { Cancelled = true };
    public static FileOperationResult ConflictResult(FileConflict conflict) => new() { Conflict = conflict };
}
