namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// The logical grouping a <see cref="ProjectSection"/> represents. Used by the UI to pick an
/// icon/label consistently instead of hardcoding them per project technology.
/// </summary>
public enum ProjectSectionKind
{
    Project = 0,
    Source,
    Tests,
    Assets,
    Configuration,
    Documentation,
    Scripts,
    Development
}
