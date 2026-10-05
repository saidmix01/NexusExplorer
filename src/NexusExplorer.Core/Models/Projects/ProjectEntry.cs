namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// A single logical item inside a <see cref="ProjectSection"/>.
/// </summary>
/// <remarks>
/// Project Explorer is a logical representation, not a copy of the file system. An entry that
/// maps to real content carries its absolute <see cref="Path"/> so the UI can navigate to or open
/// the actual file/folder in the normal explorer (the "file mapping" requirement). Command entries
/// carry a <see cref="Command"/> for Nexus Actions instead.
/// </remarks>
public sealed class ProjectEntry
{
    /// <summary>Display name shown in the section (e.g. "src", "package.json", "dev").</summary>
    public required string Name { get; init; }

    /// <summary>What this entry represents and how it should be opened.</summary>
    public required ProjectEntryKind Kind { get; init; }

    /// <summary>
    /// Absolute path to the real file or directory this entry maps to, or <c>null</c> for
    /// <see cref="ProjectEntryKind.Command"/> and <see cref="ProjectEntryKind.Information"/> entries.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// The structured command for <see cref="ProjectEntryKind.Command"/> entries, or <c>null</c>.
    /// Exposed for Nexus Actions; Project Explorer does not execute it.
    /// </summary>
    public ProjectCommand? Command { get; init; }
}
