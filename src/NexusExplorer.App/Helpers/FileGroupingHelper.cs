using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Helpers;

/// <summary>
/// Static helper for file grouping and filtering logic.
/// Extracted for testability.
/// </summary>
public static class FileGroupingHelper
{
    /// <summary>
    /// Gets the group name for an item based on its first character (Group by Name).
    /// Returns the uppercase first letter for letters, or "#" for non-letter characters.
    /// </summary>
    public static string GetNameGroup(FileSystemItem item)
    {
        if (string.IsNullOrEmpty(item.Name)) return "#";
        var firstChar = item.Name[0];
        if (char.IsLetter(firstChar))
            return char.ToUpperInvariant(firstChar).ToString();
        return "#";
    }

    /// <summary>
    /// Gets the group name for an item based on its size (Group by Size).
    /// Directories go to "Folders". Files are categorized: Empty, Small, Medium, Large, Huge.
    /// </summary>
    public static string GetSizeGroup(FileSystemItem item)
    {
        if (item.Type == FileSystemItemType.Directory) return "Folders";

        var size = item.Size ?? 0;
        if (size == 0) return "Empty";
        if (size <= 1L * 1024 * 1024) return "Small";              // 1 B - 1 MB
        if (size <= 100L * 1024 * 1024) return "Medium";           // > 1 MB - 100 MB
        if (size <= 1L * 1024 * 1024 * 1024) return "Large";      // > 100 MB - 1 GB
        return "Huge";                                              // > 1 GB
    }

    /// <summary>
    /// Filters out hidden files from the collection when showHidden is false.
    /// </summary>
    public static IEnumerable<FileSystemItem> FilterHidden(IEnumerable<FileSystemItem> items, bool showHidden)
    {
        if (showHidden) return items;
        return items.Where(i => !i.IsHidden);
    }

    /// <summary>
    /// Applies grouping to a sorted list of items, inserting group header items.
    /// </summary>
    public static IEnumerable<FileSystemItem> GroupItems(List<FileSystemItem> items, FileGroupMode mode)
    {
        if (mode == FileGroupMode.None) return items;

        var grouped = new List<FileSystemItem>();
        IEnumerable<IGrouping<string, FileSystemItem>> groups;

        switch (mode)
        {
            case FileGroupMode.Name:
                groups = items.GroupBy(GetNameGroup).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
                break;
            case FileGroupMode.DateModified:
                groups = items.GroupBy(GetDateGroup);
                var dateOrder = new[] { "Today", "Yesterday", "Earlier this week", "Last week", "Earlier this month", "Older", "Unknown" };
                groups = groups.OrderBy(g => Array.IndexOf(dateOrder, g.Key));
                break;
            case FileGroupMode.Type:
                groups = items.GroupBy(GetTypeGroup).OrderBy(g => g.Key);
                break;
            case FileGroupMode.Size:
                groups = items.GroupBy(GetSizeGroup);
                var sizeOrder = new[] { "Folders", "Empty", "Small", "Medium", "Large", "Huge" };
                groups = groups.OrderBy(g => Array.IndexOf(sizeOrder, g.Key));
                break;
            default:
                return items;
        }

        foreach (var group in groups)
        {
            grouped.Add(new FileSystemItem
            {
                IsGroupHeader = true,
                GroupName = group.Key,
                Name = group.Key,
                Path = "",
                Type = FileSystemItemType.File
            });
            grouped.AddRange(group);
        }

        return grouped;
    }

    internal static string GetDateGroup(FileSystemItem item)
    {
        if (item.LastModified is null) return "Unknown";

        var date = item.LastModified.Value.Date;
        var today = DateTime.Today;
        var diff = today - date;

        if (diff.TotalDays == 0) return "Today";
        if (diff.TotalDays == 1) return "Yesterday";
        if (diff.TotalDays < (int)today.DayOfWeek) return "Earlier this week";
        if (diff.TotalDays < (int)today.DayOfWeek + 7) return "Last week";
        if (date.Month == today.Month && date.Year == today.Year) return "Earlier this month";

        return "Older";
    }

    internal static string GetTypeGroup(FileSystemItem item)
    {
        if (item.Type == FileSystemItemType.Directory) return "Folders";

        var ext = item.Extension?.ToLowerInvariant() ?? "";
        if (new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg" }.Contains(ext)) return "Images";
        if (new[] { ".txt", ".md", ".pdf", ".doc", ".docx", ".xls", ".xlsx" }.Contains(ext)) return "Documents";
        if (new[] { ".cs", ".js", ".ts", ".html", ".css", ".json", ".xml", ".cpp", ".h" }.Contains(ext)) return "Code";
        if (new[] { ".zip", ".rar", ".7z", ".tar", ".gz" }.Contains(ext)) return "Archives";

        return "Other";
    }
}
