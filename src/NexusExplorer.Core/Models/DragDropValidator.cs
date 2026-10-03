namespace NexusExplorer.Core.Models;

/// <summary>
/// Validates drag & drop operations to prevent invalid file moves/copies.
/// Pure logic — no dependencies on UI or services.
/// </summary>
public static class DragDropValidator
{
    /// <summary>
    /// Validates whether dropping the given source paths onto the destination is allowed.
    /// </summary>
    /// <param name="sourcePaths">Paths being dragged.</param>
    /// <param name="destinationPath">Target directory path.</param>
    /// <param name="isInternal">Whether the drag originated from within Nexus (Move vs Copy).</param>
    /// <param name="sourceDirectory">The directory where the drag started (for self-drop detection).</param>
    public static DropValidationResult Validate(
        IReadOnlyList<string> sourcePaths,
        string destinationPath,
        bool isInternal,
        string? sourceDirectory)
    {
        if (sourcePaths.Count == 0)
            return DropValidationResult.Invalid("No items to drop.");

        if (string.IsNullOrEmpty(destinationPath))
            return DropValidationResult.Invalid("Invalid destination.");

        // Virtual paths cannot be drop targets
        if (VirtualPaths.IsVirtual(destinationPath))
            return DropValidationResult.Invalid("Cannot drop items on a virtual location.");

        // Check each source path for hard errors, and track which items are already in place.
        // A per-item no-op (already in the destination) must NOT invalidate the whole batch:
        // only when *every* item is already there is the drop a true no-op.
        var itemsAlreadyHere = 0;
        foreach (var source in sourcePaths)
        {
            // Dropping a folder onto itself
            if (string.Equals(source, destinationPath, StringComparison.OrdinalIgnoreCase))
                return DropValidationResult.Invalid("Cannot drop a folder into itself.");

            // Dropping a folder into one of its own descendants
            if (IsDescendant(source, destinationPath))
                return DropValidationResult.Invalid("Cannot move a folder into its own subfolder.");

            // Self-drop: item already lives in the destination directory (no-op for this item)
            var sourceParent = Path.GetDirectoryName(source);
            if (string.Equals(sourceParent, destinationPath, StringComparison.OrdinalIgnoreCase))
                itemsAlreadyHere++;
        }

        // Only a true no-op if ALL dragged items are already in the destination directory,
        // or the internal drag started in the destination and every item is already there.
        var allAlreadyHere = itemsAlreadyHere == sourcePaths.Count;
        var sameSourceDir = isInternal
            && string.Equals(sourceDirectory, destinationPath, StringComparison.OrdinalIgnoreCase);
        if (allAlreadyHere || (sameSourceDir && allAlreadyHere))
            return DropValidationResult.Invalid("Items are already in this location.");

        // Determine effect
        var effect = isInternal ? DropEffect.Move : DropEffect.Copy;
        return DropValidationResult.Valid(effect);
    }

    /// <summary>
    /// Validates whether a specific item (folder) is a valid drop target for the given sources.
    /// Used for hover-over-folder highlighting.
    /// </summary>
    public static DropValidationResult ValidateDropOnItem(
        IReadOnlyList<string> sourcePaths,
        FileSystemItem targetItem,
        bool isInternal,
        string? sourceDirectory)
    {
        // Can only drop onto directories
        if (targetItem.Type != FileSystemItemType.Directory)
            return DropValidationResult.Invalid("Cannot drop onto a file.");

        return Validate(sourcePaths, targetItem.Path, isInternal, sourceDirectory);
    }

    /// <summary>
    /// Checks if <paramref name="potentialChild"/> is a descendant of <paramref name="potentialParent"/>.
    /// </summary>
    public static bool IsDescendant(string potentialParent, string potentialChild)
    {
        var normalizedParent = NormalizePath(potentialParent);
        var normalizedChild = NormalizePath(potentialChild);

        // The child path must start with the parent path + separator
        return normalizedChild.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Canonicalizes a path for comparison: unifies separators to the platform separator,
    /// collapses repeated separators, and trims trailing separators. This prevents false
    /// negatives from mixed '/' and '\' or doubled separators (e.g. "C:\Foo\\Bar").
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var sep = Path.DirectorySeparatorChar;
        var unified = path.Replace(Path.AltDirectorySeparatorChar, sep);

        // Collapse consecutive separators into a single one.
        var doubleSep = new string(sep, 2);
        while (unified.Contains(doubleSep))
            unified = unified.Replace(doubleSep, sep.ToString());

        return unified.TrimEnd(sep);
    }
}
