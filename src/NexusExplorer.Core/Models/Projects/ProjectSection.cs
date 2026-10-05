namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// A labelled group of <see cref="ProjectEntry"/> items (e.g. SOURCE, TESTS, SCRIPTS) that
/// Project Explorer renders as one section. Sections are built from <see cref="ProjectInfo"/>,
/// never hardcoded for a specific project.
/// </summary>
public sealed class ProjectSection
{
    /// <summary>The logical kind of this section (drives the UI icon/label).</summary>
    public required ProjectSectionKind Kind { get; init; }

    /// <summary>Display title (e.g. "SOURCE"). Defaults to the uppercased <see cref="Kind"/>.</summary>
    public required string Title { get; init; }

    /// <summary>The entries contained in this section.</summary>
    public IReadOnlyList<ProjectEntry> Entries { get; init; } = [];
}
