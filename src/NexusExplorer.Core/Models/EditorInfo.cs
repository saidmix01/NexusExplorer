namespace NexusExplorer.Core.Models;

/// <summary>
/// Describes an installed code editor that can open a folder.
/// </summary>
public sealed class EditorInfo
{
    public required string Name { get; init; }

    public required string ExecutablePath { get; init; }
}
