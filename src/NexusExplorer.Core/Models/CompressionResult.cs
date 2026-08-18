namespace NexusExplorer.Core.Models;

/// <summary>
/// Result of a compression operation.
/// </summary>
public sealed class CompressionResult
{
    public bool Success { get; init; }
    public bool Cancelled { get; init; }
    public string? Error { get; init; }
    public string? ArchivePath { get; init; }
    public int ItemsProcessed { get; init; }

    public static CompressionResult Ok(string archivePath, int items) =>
        new() { Success = true, ArchivePath = archivePath, ItemsProcessed = items };

    public static CompressionResult Failed(string error) =>
        new() { Success = false, Error = error };

    public static CompressionResult CancelledResult() =>
        new() { Cancelled = true };
}
