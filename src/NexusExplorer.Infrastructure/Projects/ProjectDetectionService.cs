using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.Infrastructure.Projects;

/// <summary>
/// Runs every registered <see cref="IProjectDetector"/> against a single, shallow, root-level
/// scan of a folder and assembles a merged <see cref="ProjectInfo"/> (including display sections).
/// </summary>
/// <remarks>
/// Performance: only the root directory is enumerated (its immediate files and folders). The
/// service never recurses into large/noisy folders such as <c>node_modules</c>, <c>.git</c>,
/// <c>bin</c>, <c>obj</c>, <c>target</c>, or <c>.venv</c>. Results are cached in memory per folder
/// and can be invalidated when a manifest changes.
/// </remarks>
public sealed class ProjectDetectionService : IProjectDetectionService
{
    /// <summary>
    /// Deterministic priority used to pick the primary technology when several are detected.
    /// Earlier entries win. This ordering is stable and does not depend on detector registration.
    /// </summary>
    private static readonly ProjectType[] TypePriority =
        [ProjectType.DotNet, ProjectType.Node, ProjectType.Rust, ProjectType.Python];

    private readonly IReadOnlyList<IProjectDetector> _detectors;
    private readonly ILogger<ProjectDetectionService> _logger;
    private readonly ConcurrentDictionary<string, ProjectInfo?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ProjectDetectionService(
        IEnumerable<IProjectDetector> detectors,
        ILogger<ProjectDetectionService>? logger = null)
    {
        _detectors = detectors.ToList();
        _logger = logger ?? NullLogger<ProjectDetectionService>.Instance;
    }

    public async Task<ProjectInfo?> DetectAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return null;

        var key = NormalizeKey(folderPath);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        // The scan touches the disk; keep it off the UI thread.
        var info = await Task.Run(() => DetectCore(folderPath, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

        _cache[key] = info;
        return info;
    }

    public void Invalidate(string? folderPath = null)
    {
        if (folderPath is null)
        {
            _cache.Clear();
            return;
        }
        _cache.TryRemove(NormalizeKey(folderPath), out _);
    }

    private ProjectInfo? DetectCore(string folderPath, CancellationToken ct)
    {
        var context = ProbeRoot(folderPath, ct);
        if (context is null)
            return null;

        // Run every detector against the shared probe. Multiple may match (multi-technology).
        var detections = new List<ProjectDetection>();
        foreach (var detector in _detectors)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (detector.Detect(context) is { } detection)
                    detections.Add(detection);
            }
            catch (Exception ex)
            {
                // A misbehaving detector must not break detection for the whole folder.
                _logger.LogWarning(ex, "Detector {Detector} failed for {Path}.", detector.Type, folderPath);
            }
        }

        if (detections.Count == 0)
            return null;

        return BuildProjectInfo(folderPath, context, detections);
    }

    /// <summary>
    /// Enumerates the immediate children of the folder once. Returns <c>null</c> if the folder is
    /// missing or unreadable. This is the only disk access performed during detection.
    /// </summary>
    private ProjectProbeContext? ProbeRoot(string folderPath, CancellationToken ct)
    {
        if (!Directory.Exists(folderPath))
            return null;

        try
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // EnumerateFileSystemEntries is lazy and top-level only — no recursion into big folders.
            foreach (var entry in Directory.EnumerateFileSystemEntries(folderPath))
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(entry);
                if (string.IsNullOrEmpty(name))
                    continue;

                if (Directory.Exists(entry))
                    dirs.Add(name);
                else
                    files.Add(name);
            }

            return new ProjectProbeContext
            {
                RootPath = folderPath,
                FileNames = files,
                DirectoryNames = dirs
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogDebug(ex, "Could not probe folder {Path}.", folderPath);
            return null;
        }
    }

    private ProjectInfo BuildProjectInfo(
        string folderPath, ProjectProbeContext context, List<ProjectDetection> detections)
    {
        // Order detections by the deterministic priority so the primary type and the technology
        // list are stable regardless of detector registration order.
        var ordered = detections
            .OrderBy(d => Array.IndexOf(TypePriority, d.Type) is var i && i >= 0 ? i : int.MaxValue)
            .ToList();

        var primary = ordered[0].Type;
        var technologies = ordered.Select(d => d.Type).ToList();

        // Prefer a name reported by a detector (e.g. package.json "name"), honoring priority order.
        var name = ordered.Select(d => d.ProjectName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                   ?? ResolveFolderName(folderPath);

        var projectFiles = Flatten(ordered, d => d.ProjectFiles);
        var sourceDirs = Flatten(ordered, d => d.SourceDirectories);
        var testDirs = Flatten(ordered, d => d.TestDirectories);
        var assetDirs = Flatten(ordered, d => d.AssetDirectories);
        var configFiles = Flatten(ordered, d => d.ConfigurationFiles);
        var docFiles = Flatten(ordered, d => d.DocumentationFiles);
        var commands = ordered.SelectMany(d => d.Commands).ToList();

        var git = DetectGit(context);

        var sections = ProjectSectionBuilder.Build(
            projectFiles, sourceDirs, testDirs, assetDirs, configFiles, docFiles, commands, git);

        return new ProjectInfo
        {
            Name = name,
            RootPath = folderPath,
            PrimaryType = primary,
            DetectedTechnologies = technologies,
            ProjectFiles = projectFiles,
            SourceDirectories = sourceDirs,
            TestDirectories = testDirs,
            AssetDirectories = assetDirs,
            ConfigurationFiles = configFiles,
            DocumentationFiles = docFiles,
            Commands = commands,
            Git = git,
            Sections = sections
        };
    }

    private static ProjectGitInfo? DetectGit(ProjectProbeContext context) =>
        context.HasDirectory(".git") ? new ProjectGitInfo { HasRepository = true } : null;

    /// <summary>Combines a per-detection list across detections, de-duplicating while keeping order.</summary>
    private static List<string> Flatten(
        IEnumerable<ProjectDetection> detections, Func<ProjectDetection, IReadOnlyList<string>> selector)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var detection in detections)
        {
            foreach (var value in selector(detection))
            {
                if (seen.Add(value))
                    result.Add(value);
            }
        }
        return result;
    }

    private static string ResolveFolderName(string folderPath)
    {
        var name = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? folderPath : name;
    }

    private static string NormalizeKey(string folderPath) =>
        folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
