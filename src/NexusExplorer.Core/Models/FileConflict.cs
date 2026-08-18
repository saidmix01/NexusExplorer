namespace NexusExplorer.Core.Models;

/// <summary>
/// Represents a file conflict encountered during a file operation.
/// </summary>
public sealed class FileConflict
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public required string FileName { get; init; }
    public bool IsDirectory { get; init; }
    public long? SourceSize { get; init; }
    public long? DestinationSize { get; init; }
    public DateTime? SourceModified { get; init; }
    public DateTime? DestinationModified { get; init; }
}
