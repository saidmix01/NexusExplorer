using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.Infrastructure.Projects.Detectors;

/// <summary>
/// Detects .NET projects by solution/project manifest files at the root
/// (<c>*.sln</c>, <c>*.slnx</c>, <c>*.csproj</c>, <c>*.fsproj</c>, <c>*.vbproj</c>).
/// </summary>
public sealed class DotNetProjectDetector : IProjectDetector
{
    private static readonly string[] SolutionExtensions = [".sln", ".slnx"];
    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];

    private static readonly string[] SourceDirs = ["src", "source"];
    private static readonly string[] TestDirs = ["tests", "test"];
    private static readonly string[] ConfigFiles =
        ["global.json", "Directory.Build.props", "Directory.Build.targets",
         "nuget.config", ".editorconfig"];

    public ProjectType Type => ProjectType.DotNet;

    public ProjectDetection? Detect(ProjectProbeContext context)
    {
        var manifests = context
            .FilesWithExtension([.. SolutionExtensions, .. ProjectExtensions])
            .Select(f => Path.Combine(context.RootPath, f))
            .ToList();

        if (manifests.Count == 0)
            return null;

        // Prefer the solution name, else the first project file name, as the project name.
        var primaryManifest = context
            .FilesWithExtension(SolutionExtensions)
            .Concat(context.FilesWithExtension(ProjectExtensions))
            .FirstOrDefault();
        var name = primaryManifest is not null
            ? Path.GetFileNameWithoutExtension(primaryManifest)
            : null;

        return new ProjectDetection
        {
            Type = ProjectType.DotNet,
            ProjectName = name,
            ProjectFiles = manifests,
            SourceDirectories = DetectorConventions.ResolveDirectories(context, SourceDirs),
            TestDirectories = DetectorConventions.ResolveDirectories(context, TestDirs),
            ConfigurationFiles = DetectorConventions.ResolveFiles(context, ConfigFiles),
            DocumentationFiles = DetectorConventions.ResolveDocumentationFiles(context),
            Commands = BuildCommands(context.RootPath)
        };
    }

    private static List<ProjectCommand> BuildCommands(string rootPath) =>
    [
        Command("restore", rootPath),
        Command("build", rootPath),
        Command("run", rootPath),
        Command("test", rootPath)
    ];

    private static ProjectCommand Command(string verb, string rootPath) => new()
    {
        Label = $"dotnet {verb}",
        Executable = "dotnet",
        Arguments = [verb],
        WorkingDirectory = rootPath,
        Source = ProjectCommandSource.Tooling
    };
}
