namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// The outcome produced by a single <c>IProjectDetector</c> for one folder. A detector returns
/// <c>null</c> when it does not recognize the folder as its kind of project.
/// </summary>
/// <remarks>
/// A detector reports only what it can determine reliably from the root-level probe. The
/// detection service merges results from all detectors into a single <see cref="ProjectInfo"/>
/// and builds the display sections, so individual detectors stay small and focused.
/// </remarks>
public sealed class ProjectDetection
{
    /// <summary>The technology this detection represents.</summary>
    public required ProjectType Type { get; init; }

    /// <summary>Absolute paths of the manifest/project files this detector matched.</summary>
    public IReadOnlyList<string> ProjectFiles { get; init; } = [];

    /// <summary>Absolute paths of source directories this detector recognized.</summary>
    public IReadOnlyList<string> SourceDirectories { get; init; } = [];

    /// <summary>Absolute paths of test directories this detector recognized.</summary>
    public IReadOnlyList<string> TestDirectories { get; init; } = [];

    /// <summary>Absolute paths of asset directories this detector recognized.</summary>
    public IReadOnlyList<string> AssetDirectories { get; init; } = [];

    /// <summary>Absolute paths of configuration files this detector recognized.</summary>
    public IReadOnlyList<string> ConfigurationFiles { get; init; } = [];

    /// <summary>Absolute paths of documentation files this detector recognized.</summary>
    public IReadOnlyList<string> DocumentationFiles { get; init; } = [];

    /// <summary>Structured commands this detector can expose for Nexus Actions.</summary>
    public IReadOnlyList<ProjectCommand> Commands { get; init; } = [];

    /// <summary>
    /// Optional project name read from this technology's metadata (e.g. the "name" field in
    /// package.json). <c>null</c> when unavailable; the service falls back to the folder name.
    /// </summary>
    public string? ProjectName { get; init; }
}
