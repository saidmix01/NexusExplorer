namespace NexusExplorer.Core.Models;

/// <summary>
/// Represents a "Send To" destination.
/// </summary>
public sealed class SendToTarget
{
    /// <summary>
    /// Display name of the target (e.g., "Desktop", "Documents").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The resolved path of the target folder, or null for special targets.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// The type of the Send To target.
    /// </summary>
    public SendToTargetType Type { get; init; }
}

/// <summary>
/// Type of Send To destination.
/// </summary>
public enum SendToTargetType
{
    /// <summary>
    /// A regular folder destination (copy files to it).
    /// </summary>
    Folder,

    /// <summary>
    /// Compress into a ZIP archive at the source location.
    /// </summary>
    CompressedFolder
}
