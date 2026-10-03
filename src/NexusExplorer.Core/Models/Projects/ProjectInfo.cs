namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// A logical description of a software project detected at a folder, built from a fast,
/// local, deterministic scan of the root level only.
/// </summary>
/// <remarks>
/// This is a read-only snapshot. It never mutates the project on disk. All paths are absolute so
/// the UI can map a logical entry back to the real file/folder. Collections default to empty so
/// the UI can enumerate without null checks.
/// </remarks>
public sealed class ProjectInfo
{
    /// <summary>Project display name (usually the root folder name, or metadata name when available).</summary>
    public required string Name { get; init; }

    /// <summary>Absolute path to the project root folder.</summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// The primary technology chosen deterministically when several are present
    /// (see the detection service for the ordering rule).
    /// </summary>
    public required ProjectType PrimaryType { get; init; }

    /// <summary>
    /// All technologies detected at the root. May contain more than one; the project is not
    /// assumed to belong to a single ecosystem. Ordered by detection priority.
    /// </summary>
    public IReadOnlyList<ProjectType> DetectedTechnologies { get; init; } = [];

    /// <summary>Absolute paths of the key project/manifest files (e.g. package.json, *.csproj, Cargo.toml).</summary>
    public IReadOnlyList<string> ProjectFiles { get; init; } = [];

    /// <summary>Absolute paths of detected source directories (e.g. src).</summary>
    public IReadOnlyList<string> SourceDirectories { get; init; } = [];

    /// <summary>Absolute paths of detected test directories (e.g. tests, test).</summary>
    public IReadOnlyList<string> TestDirectories { get; init; } = [];

    /// <summary>Absolute paths of detected asset directories (e.g. public, assets, static).</summary>
    public IReadOnlyList<string> AssetDirectories { get; init; } = [];

    /// <summary>Absolute paths of configuration files (e.g. tsconfig.json, .editorconfig).</summary>
    public IReadOnlyList<string> ConfigurationFiles { get; init; } = [];

    /// <summary>Absolute paths of documentation files (e.g. README.md, LICENSE).</summary>
    public IReadOnlyList<string> DocumentationFiles { get; init; } = [];

    /// <summary>
    /// Structured commands exposed for Nexus Actions (scripts and fixed tooling commands).
    /// Project Explorer surfaces these but does not execute them.
    /// </summary>
    public IReadOnlyList<ProjectCommand> Commands { get; init; } = [];

    /// <summary>Git information if a repository is present at the root; otherwise <c>null</c>.</summary>
    public ProjectGitInfo? Git { get; init; }

    /// <summary>
    /// Pre-built logical sections ready for the UI to render. Constructed from the data above by
    /// the detection service so Views contain no grouping logic.
    /// </summary>
    public IReadOnlyList<ProjectSection> Sections { get; init; } = [];
}
