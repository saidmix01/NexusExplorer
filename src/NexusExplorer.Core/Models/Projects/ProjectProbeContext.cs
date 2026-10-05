namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// A read-only snapshot of a folder's root level, captured once and shared with every
/// <c>IProjectDetector</c> so detection stays fast and never recurses the whole disk.
/// </summary>
/// <remarks>
/// All file and directory names are the top-level entry names only (not full paths), compared
/// case-insensitively. Detectors must not touch the file system directly; they work from this
/// context. Reading the contents of a specific manifest (e.g. package.json) is allowed via the
/// absolute <see cref="RootPath"/> but should remain shallow and safe.
/// </remarks>
public sealed class ProjectProbeContext
{
    /// <summary>Absolute path to the folder being probed.</summary>
    public required string RootPath { get; init; }

    /// <summary>Top-level file names present at the root (case-insensitive lookups).</summary>
    public required IReadOnlySet<string> FileNames { get; init; }

    /// <summary>Top-level directory names present at the root (case-insensitive lookups).</summary>
    public required IReadOnlySet<string> DirectoryNames { get; init; }

    /// <summary>Returns true if a top-level file with the given name exists.</summary>
    public bool HasFile(string name) => FileNames.Contains(name);

    /// <summary>Returns true if a top-level directory with the given name exists.</summary>
    public bool HasDirectory(string name) => DirectoryNames.Contains(name);

    /// <summary>Returns true if any top-level file has one of the given extensions (e.g. ".csproj").</summary>
    public bool HasFileWithExtension(params string[] extensions)
    {
        foreach (var file in FileNames)
        {
            var ext = System.IO.Path.GetExtension(file);
            foreach (var candidate in extensions)
            {
                if (string.Equals(ext, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>Enumerates top-level file names that have one of the given extensions.</summary>
    public IEnumerable<string> FilesWithExtension(params string[] extensions)
    {
        foreach (var file in FileNames)
        {
            var ext = System.IO.Path.GetExtension(file);
            foreach (var candidate in extensions)
            {
                if (string.Equals(ext, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                    break;
                }
            }
        }
    }
}
