using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.Infrastructure.Projects.Detectors;

/// <summary>
/// Detects Rust projects by the presence of a <c>Cargo.toml</c> at the root.
/// </summary>
public sealed class RustProjectDetector : IProjectDetector
{
    private const string ManifestName = "Cargo.toml";

    private static readonly string[] SourceDirs = ["src"];
    private static readonly string[] TestDirs = ["tests"];
    private static readonly string[] ConfigFiles = ["rustfmt.toml", ".rustfmt.toml", "rust-toolchain.toml", "rust-toolchain"];

    public ProjectType Type => ProjectType.Rust;

    public ProjectDetection? Detect(ProjectProbeContext context)
    {
        if (!context.HasFile(ManifestName))
            return null;

        var projectFiles = DetectorConventions.ResolveFiles(context, ManifestName, "Cargo.lock");
        // The project name lives in Cargo.toml's [package] section; parsing TOML reliably needs a
        // dependency we don't have, so we fall back to the folder name (handled by the service).

        return new ProjectDetection
        {
            Type = ProjectType.Rust,
            ProjectFiles = projectFiles,
            SourceDirectories = DetectorConventions.ResolveDirectories(context, SourceDirs),
            TestDirectories = DetectorConventions.ResolveDirectories(context, TestDirs),
            ConfigurationFiles = DetectorConventions.ResolveFiles(context, ConfigFiles),
            DocumentationFiles = DetectorConventions.ResolveDocumentationFiles(context),
            Commands = BuildCommands(context.RootPath)
        };
    }

    private static List<ProjectCommand> BuildCommands(string rootPath) =>
    [
        Command("run", rootPath),
        Command("build", rootPath),
        Command("test", rootPath),
        Command("check", rootPath),
        Command("fmt", rootPath)
    ];

    private static ProjectCommand Command(string verb, string rootPath) => new()
    {
        Label = $"cargo {verb}",
        Executable = "cargo",
        Arguments = [verb],
        WorkingDirectory = rootPath,
        Source = ProjectCommandSource.Tooling
    };
}
