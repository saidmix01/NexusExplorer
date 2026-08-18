using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Helpers;

/// <summary>
/// Helper to determine the working directory for "Open in Terminal" commands.
/// </summary>
public static class TerminalPathHelper
{
    /// <summary>
    /// Determines the target working directory based on a selected item.
    /// - Directory: uses the directory's path directly.
    /// - File: uses the file's parent directory.
    /// - Null item: returns null (caller should use CurrentPath).
    /// </summary>
    public static string? GetTerminalWorkingDirectory(FileSystemItem? item, string currentPath)
    {
        if (item is null)
            return string.IsNullOrWhiteSpace(currentPath) ? null : currentPath;

        if (item.Type == FileSystemItemType.Directory)
            return item.Path;

        // For files, use the parent directory
        var parent = Path.GetDirectoryName(item.Path);
        return !string.IsNullOrEmpty(parent) ? parent : currentPath;
    }
}
