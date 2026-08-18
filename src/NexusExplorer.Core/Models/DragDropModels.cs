namespace NexusExplorer.Core.Models;

/// <summary>
/// Represents a drag & drop operation with source paths, intended effect, and origin.
/// </summary>
public sealed class DragDropOperation
{
    /// <summary>
    /// Full paths of all dragged items.
    /// </summary>
    public required IReadOnlyList<string> SourcePaths { get; init; }

    /// <summary>
    /// Whether this drag originated from within Nexus Explorer.
    /// Internal drags default to Move; external drags default to Copy.
    /// </summary>
    public bool IsInternal { get; init; }

    /// <summary>
    /// The directory where the drag originated (for internal drags).
    /// </summary>
    public string? SourceDirectory { get; init; }
}

/// <summary>
/// Result of validating a drop target.
/// </summary>
public sealed class DropValidationResult
{
    public bool IsValid { get; init; }
    public string? Reason { get; init; }
    public DropEffect Effect { get; init; }

    public static DropValidationResult Valid(DropEffect effect) => new() { IsValid = true, Effect = effect };
    public static DropValidationResult Invalid(string reason) => new() { IsValid = false, Reason = reason, Effect = DropEffect.None };
}

/// <summary>
/// The effect of the drop operation.
/// </summary>
public enum DropEffect
{
    None,
    Copy,
    Move
}
