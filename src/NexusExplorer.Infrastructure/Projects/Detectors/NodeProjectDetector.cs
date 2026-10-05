using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.Infrastructure.Projects.Detectors;

/// <summary>
/// Detects Node.js projects by the presence of a <c>package.json</c> at the root.
/// Reads the manifest safely to surface the project name and runnable scripts.
/// </summary>
public sealed class NodeProjectDetector : IProjectDetector
{
    private const string ManifestName = "package.json";

    // Common Node source/test/asset/config/doc layout conventions.
    private static readonly string[] SourceDirs = ["src", "lib", "app"];
    private static readonly string[] TestDirs = ["test", "tests", "__tests__", "spec"];
    private static readonly string[] AssetDirs = ["public", "static", "assets"];
    private static readonly string[] ConfigFiles =
        ["tsconfig.json", "jsconfig.json", ".eslintrc", ".eslintrc.json", ".eslintrc.js",
         ".prettierrc", ".babelrc", "vite.config.js", "vite.config.ts", "webpack.config.js",
         ".npmrc", ".nvmrc"];

    private readonly ILogger<NodeProjectDetector> _logger;

    public NodeProjectDetector(ILogger<NodeProjectDetector>? logger = null)
    {
        _logger = logger ?? NullLogger<NodeProjectDetector>.Instance;
    }

    public ProjectType Type => ProjectType.Node;

    public ProjectDetection? Detect(ProjectProbeContext context)
    {
        if (!context.HasFile(ManifestName))
            return null;

        var manifestPath = Path.Combine(context.RootPath, ManifestName);
        var (name, scripts) = ReadManifest(manifestPath);

        var projectFiles = new List<string> { manifestPath };
        // Lock files are useful signals/metadata but optional.
        foreach (var lockFile in new[] { "package-lock.json", "yarn.lock", "pnpm-lock.yaml" })
        {
            if (context.HasFile(lockFile))
                projectFiles.Add(Path.Combine(context.RootPath, lockFile));
        }

        var commands = BuildCommands(context.RootPath, scripts);

        return new ProjectDetection
        {
            Type = ProjectType.Node,
            ProjectName = name,
            ProjectFiles = projectFiles,
            SourceDirectories = DetectorConventions.ResolveDirectories(context, SourceDirs),
            TestDirectories = DetectorConventions.ResolveDirectories(context, TestDirs),
            AssetDirectories = DetectorConventions.ResolveDirectories(context, AssetDirs),
            ConfigurationFiles = DetectorConventions.ResolveFiles(context, ConfigFiles),
            DocumentationFiles = DetectorConventions.ResolveDocumentationFiles(context),
            Commands = commands
        };
    }

    /// <summary>
    /// Reads package.json defensively. Returns the "name" and ordered "scripts" when present.
    /// Never throws: malformed or unreadable manifests yield empty results.
    /// </summary>
    private (string? Name, IReadOnlyList<string> Scripts) ReadManifest(string manifestPath)
    {
        try
        {
            // package.json files are tiny; a shallow read is safe and fast.
            using var stream = File.OpenRead(manifestPath);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return (null, []);

            string? name = null;
            if (root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                name = nameEl.GetString();

            var scripts = new List<string>();
            if (root.TryGetProperty("scripts", out var scriptsEl) && scriptsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in scriptsEl.EnumerateObject())
                    scripts.Add(prop.Name);
            }

            return (string.IsNullOrWhiteSpace(name) ? null : name, scripts);
        }
        catch (Exception ex)
        {
            // Invalid JSON, permission denied, etc. — detection still succeeds on file presence.
            _logger.LogDebug(ex, "Could not parse {Manifest}; continuing without name/scripts.", manifestPath);
            return (null, []);
        }
    }

    private static List<ProjectCommand> BuildCommands(string rootPath, IReadOnlyList<string> scripts)
    {
        var commands = new List<ProjectCommand>
        {
            new()
            {
                Label = "install",
                Executable = "npm",
                Arguments = ["install"],
                WorkingDirectory = rootPath,
                Source = ProjectCommandSource.Tooling
            }
        };

        // Each package.json script becomes "npm run <script>" (except the special lifecycle names
        // start/test which npm allows without "run").
        foreach (var script in scripts)
        {
            var args = script is "start" or "test"
                ? new[] { script }
                : ["run", script];

            commands.Add(new ProjectCommand
            {
                Label = script,
                Executable = "npm",
                Arguments = args,
                WorkingDirectory = rootPath,
                Source = ProjectCommandSource.Script
            });
        }

        return commands;
    }
}
