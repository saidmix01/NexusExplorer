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
    public double Percentage
    {
        get
        {
            var raw = TotalBytes > 0 ? (double)BytesProcessed / TotalBytes * 100 :
                      TotalItems > 0 ? (double)CurrentItemIndex / TotalItems * 100 : 0;
            // Clamp: byte totals can be slightly off (files changing, undercount), which must
            // never produce absurd percentages like 20000%.
            return raw < 0 ? 0 : raw > 100 ? 100 : raw;
        }
    }
    public double BytesPerSecond { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsCancelled { get; init; }
    public string? Error { get; init; }
}
