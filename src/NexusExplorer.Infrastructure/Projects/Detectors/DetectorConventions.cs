using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.Infrastructure.Projects.Detectors;

/// <summary>
/// Shared, technology-agnostic layout conventions and small resolution helpers used by the
/// individual <c>IProjectDetector</c> implementations. Keeps each detector focused on what is
/// specific to its ecosystem and avoids duplicating the common "map a convention name to an
/// absolute path if it exists" logic.
/// </summary>
internal static class DetectorConventions
{
    /// <summary>Common documentation file names recognized across ecosystems (case-insensitive).</summary>
    public static readonly string[] DocumentationFiles =
        ["readme.md", "readme", "readme.txt", "readme.rst",
         "license", "license.md", "license.txt",
         "changelog.md", "changelog", "contributing.md", "authors", "notice"];

    /// <summary>Returns absolute paths for each candidate directory name that exists at the root.</summary>
    public static List<string> ResolveDirectories(ProjectProbeContext ctx, params string[] candidates)
    {
        var result = new List<string>();
        foreach (var dir in candidates)
        {
            if (ctx.HasDirectory(dir))
                result.Add(Path.Combine(ctx.RootPath, dir));
        }
        return result;
    }

    /// <summary>Returns absolute paths for each candidate file name that exists at the root.</summary>
    public static List<string> ResolveFiles(ProjectProbeContext ctx, params string[] candidates)
    {
        var result = new List<string>();
        foreach (var file in candidates)
        {
            if (ctx.HasFile(file))
                result.Add(Path.Combine(ctx.RootPath, file));
        }
        return result;
    }

    /// <summary>Returns absolute paths of root-level documentation files.</summary>
    public static List<string> ResolveDocumentationFiles(ProjectProbeContext ctx)
    {
        var result = new List<string>();
        foreach (var file in ctx.FileNames)
        {
            if (Array.Exists(DocumentationFiles, d => string.Equals(d, file, StringComparison.OrdinalIgnoreCase)))
                result.Add(Path.Combine(ctx.RootPath, file));
        }
        return result;
    }
}
