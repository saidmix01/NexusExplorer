using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.Infrastructure.Projects.Detectors;

/// <summary>
/// Detects Python projects by any of <c>pyproject.toml</c>, <c>requirements.txt</c>, or
/// <c>setup.py</c> at the root.
/// </summary>
/// <remarks>
/// Python has no single canonical package manager, so this detector only exposes commands it can
/// determine reliably from the files present (e.g. a <c>requirements.txt</c> implies a safe
/// <c>pip install -r requirements.txt</c>). It never guesses an environment manager.
/// </remarks>
public sealed class PythonProjectDetector : IProjectDetector
{
    private const string PyProject = "pyproject.toml";
    private const string Requirements = "requirements.txt";
    private const string SetupPy = "setup.py";
    private const string SetupCfg = "setup.cfg";

    private static readonly string[] Manifests = [PyProject, Requirements, SetupPy];
    private static readonly string[] SourceDirs = ["src", "app"];
    private static readonly string[] TestDirs = ["tests", "test"];
    private static readonly string[] ConfigFiles =
        [SetupCfg, "tox.ini", "pytest.ini", "Pipfile", "poetry.lock", "Pipfile.lock",
         ".flake8", "mypy.ini", "conftest.py"];

    public ProjectType Type => ProjectType.Python;

    public ProjectDetection? Detect(ProjectProbeContext context)
    {
        var presentManifests = Manifests.Where(context.HasFile).ToList();
        if (presentManifests.Count == 0)
            return null;

        var projectFiles = presentManifests
            .Select(m => Path.Combine(context.RootPath, m))
            .ToList();

        // "docs" is a very common Python documentation directory; surface it as a source of docs
        // by also treating it as an asset-free doc location via the service's section builder.
        var sourceDirs = DetectorConventions.ResolveDirectories(context, SourceDirs);

        return new ProjectDetection
        {
            Type = ProjectType.Python,
            ProjectFiles = projectFiles,
            SourceDirectories = sourceDirs,
            TestDirectories = DetectorConventions.ResolveDirectories(context, TestDirs),
            ConfigurationFiles = DetectorConventions.ResolveFiles(context, ConfigFiles),
            DocumentationFiles = DetectorConventions.ResolveDocumentationFiles(context),
            Commands = BuildCommands(context)
        };
    }

    private static List<ProjectCommand> BuildCommands(ProjectProbeContext context)
    {
        var commands = new List<ProjectCommand>();

        // Only emit commands we can determine reliably from the files present.
        if (context.HasFile(Requirements))
        {
            commands.Add(new ProjectCommand
            {
                Label = "install requirements",
                Executable = "pip",
                Arguments = ["install", "-r", Requirements],
                WorkingDirectory = context.RootPath,
                Source = ProjectCommandSource.Tooling
            });
        }

        // A pyproject.toml or setup.py means the project is installable in editable mode.
        if (context.HasFile(PyProject) || context.HasFile(SetupPy))
        {
            commands.Add(new ProjectCommand
            {
                Label = "install (editable)",
                Executable = "pip",
                Arguments = ["install", "-e", "."],
                WorkingDirectory = context.RootPath,
                Source = ProjectCommandSource.Tooling
            });
        }

        // pytest is the de-facto test runner; only suggest it when a tests dir or pytest config exists.
        if (context.HasDirectory("tests") || context.HasDirectory("test")
            || context.HasFile("pytest.ini") || context.HasFile("conftest.py"))
        {
            commands.Add(new ProjectCommand
            {
                Label = "test",
                Executable = "pytest",
                Arguments = [],
                WorkingDirectory = context.RootPath,
                Source = ProjectCommandSource.Tooling
            });
        }

        return commands;
    }
}
