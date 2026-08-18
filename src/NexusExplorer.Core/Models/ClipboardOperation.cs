namespace NexusExplorer.Core.Models;

/// <summary>
/// Represents items stored in the file clipboard (cut or copy).
/// </summary>
public sealed class ClipboardOperation
{
    public required IReadOnlyList<string> Paths { get; init; }
    public required bool IsCut { get; init; }
}
