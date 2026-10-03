using System.IO;

namespace NexusExplorer.App.Helpers;

/// <summary>
/// Parses command-line arguments passed by the shell / other apps (e.g. VS Code's
/// "Reveal in File Explorer", or a folder double-click) into a directory to open.
/// </summary>
public static class LaunchArgsHelper
{
    /// <summary>
    /// Resolves the folder to open from raw process arguments. Handles:
    ///  - a plain folder path            → that folder
    ///  - a file path                    → the file's containing folder
    ///  - Explorer's "/select,&lt;path&gt;" form (used by VS Code/Windows to reveal an item)
    ///  - surrounding quotes and stray whitespace
    /// Returns null when no usable path is present.
    /// </summary>
    public static string? ResolveTargetDirectory(string[] args)
    {
        if (args is null || args.Length == 0) return null;

        // Reassemble because an unquoted path with spaces can arrive split across args.
        var raw = string.Join(" ", args).Trim();
        return ResolveTargetDirectory(raw);
    }

    /// <summary>Resolves the folder to open from a single raw argument string.</summary>
    public static string? ResolveTargetDirectory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var value = raw.Trim();

        // Explorer-style reveal: "/select,C:\path\item" (comma-separated, case-insensitive).
        if (value.StartsWith("/select", StringComparison.OrdinalIgnoreCase))
        {
            var comma = value.IndexOf(',');
            value = comma >= 0 ? value[(comma + 1)..] : string.Empty;
        }

        value = value.Trim().Trim('"').Trim();
        if (string.IsNullOrEmpty(value)) return null;

        try
        {
            if (Directory.Exists(value))
                return value;

            if (File.Exists(value))
                return Path.GetDirectoryName(value);

            // A path that no longer exists but whose parent does (e.g. reveal of a deleted item).
            var parent = Path.GetDirectoryName(value);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                return parent;
        }
        catch
        {
            // Malformed path — ignore.
        }

        return null;
    }
}
