namespace NexusExplorer.Core.Models;

/// <summary>
/// Reports progress of a file operation.
/// </summary>
public sealed class FileOperationProgress
{
    public required FileOperationType OperationType { get; init; }
    public string? CurrentItem { get; init; }
    public int CurrentItemIndex { get; init; }
    public int TotalItems { get; init; }
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
    public double Percentage => TotalBytes > 0 ? (double)BytesProcessed / TotalBytes * 100 : 
                                TotalItems > 0 ? (double)CurrentItemIndex / TotalItems * 100 : 0;
    public double BytesPerSecond { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsCancelled { get; init; }
    public string? Error { get; init; }
}
