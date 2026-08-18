using System.Globalization;
using Avalonia.Data.Converters;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Converts a FileSystemItem to a short info string:
/// - Files: displays human-readable size (e.g., "4.2 MB")
/// - Directories: displays "Folder" (item count loaded async elsewhere)
/// - Drives: displays drive letter
/// </summary>
public sealed class ItemInfoConverter : IValueConverter
{
    public static readonly ItemInfoConverter Instance = new();

    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FileSystemItem item)
            return string.Empty;

        if (item.IsGroupHeader)
            return string.Empty;

        if (item.Type == FileSystemItemType.Directory)
            return "Folder";

        if (item.Type == FileSystemItemType.Drive)
            return item.Path;

        if (item.Size is long size and > 0)
            return FormatSize(size);

        return string.Empty;
    }

    private static string FormatSize(long size)
    {
        var unitIndex = 0;
        var displaySize = (double)size;

        while (displaySize >= 1024 && unitIndex < Units.Length - 1)
        {
            displaySize /= 1024;
            unitIndex++;
        }

        return $"{displaySize:0.#} {Units[unitIndex]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a DriveItem's space information to a display string like "285 GB free of 475 GB".
/// </summary>
public sealed class DriveSpaceConverter : IValueConverter
{
    public static readonly DriveSpaceConverter Instance = new();

    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FileSystemItem item || item.Type != FileSystemItemType.Drive)
            return string.Empty;

        if (item.Size is not long totalSize || totalSize <= 0)
            return "Unavailable";

        // For drives displayed as FileSystemItems, Size = TotalSize
        // We store free space info differently — this converter is for basic display
        return FormatSize(totalSize) + " total";
    }

    private static string FormatSize(long size)
    {
        var unitIndex = 0;
        var displaySize = (double)size;

        while (displaySize >= 1024 && unitIndex < Units.Length - 1)
        {
            displaySize /= 1024;
            unitIndex++;
        }

        return $"{displaySize:0.#} {Units[unitIndex]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
