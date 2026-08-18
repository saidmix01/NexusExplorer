namespace NexusExplorer.Core.Models;

/// <summary>
/// Lifecycle state of a file operation managed by the File Operation Center.
/// </summary>
public enum FileOperationStatus
{
    Pending,
    Running,
    Paused,
    Completed,
    Cancelled,
    Failed
}
