using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

public sealed class IsDetailsViewConverter : IValueConverter
{
    public static readonly IsDetailsViewConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExplorerViewMode.Details;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IsListViewConverter : IValueConverter
{
    public static readonly IsListViewConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExplorerViewMode.List;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IsExtraLargeIconsViewConverter : IValueConverter
{
    public static readonly IsExtraLargeIconsViewConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExplorerViewMode.ExtraLargeIcons;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class IsLargeIconsViewConverter : IValueConverter
{
    public static readonly IsLargeIconsViewConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExplorerViewMode.LargeIcons;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class IsMediumIconsViewConverter : IValueConverter
{
    public static readonly IsMediumIconsViewConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExplorerViewMode.MediumIcons;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class IsSmallIconsViewConverter : IValueConverter
{
    public static readonly IsSmallIconsViewConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExplorerViewMode.SmallIcons;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class IsIconViewConverter : IValueConverter
{
    public static readonly IsIconViewConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExplorerViewMode.ExtraLargeIcons or ExplorerViewMode.LargeIcons or ExplorerViewMode.MediumIcons or ExplorerViewMode.SmallIcons;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ViewModeToIconSizeConverter : IValueConverter
{
    public static readonly ViewModeToIconSizeConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExplorerViewMode mode)
        {
            return mode switch
            {
                ExplorerViewMode.ExtraLargeIcons => 96,
                ExplorerViewMode.LargeIcons => 64,
                ExplorerViewMode.MediumIcons => 48,
                ExplorerViewMode.SmallIcons => 32,
                _ => 48
            };
        }
        return 48;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Multi-value converter that computes icon size from ViewMode and IconZoomLevel.
/// Bindings: [0] = ViewMode (ExplorerViewMode), [1] = IconZoomLevel (double 0-100).
/// The zoom interpolates icon size: 0=24px, 50=base size for mode, 100=128px.
/// </summary>
public sealed class ZoomAwareIconSizeConverter : IMultiValueConverter
{
    public static readonly ZoomAwareIconSizeConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return 48;

        var mode = values[0] is ExplorerViewMode m ? m : ExplorerViewMode.MediumIcons;
        var zoom = values[1] is double z ? z : 50.0;

        // Base sizes per mode
        double baseSize = mode switch
        {
            ExplorerViewMode.ExtraLargeIcons => 96.0,
            ExplorerViewMode.LargeIcons => 64.0,
            ExplorerViewMode.MediumIcons => 48.0,
            ExplorerViewMode.SmallIcons => 32.0,
            _ => 48.0
        };

        // Zoom factor: 0 → 0.5x, 50 → 1.0x, 100 → 2.0x
        var factor = 0.5 + (zoom / 100.0) * 1.5;
        var size = (int)Math.Clamp(baseSize * factor, 16.0, 256.0);
        return size;
    }
}

/// <summary>
/// Multi-value converter for item container width based on ViewMode and IconZoomLevel.
/// </summary>
public sealed class ZoomAwareItemWidthConverter : IMultiValueConverter
{
    public static readonly ZoomAwareItemWidthConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return 80.0;

        var mode = values[0] is ExplorerViewMode m ? m : ExplorerViewMode.MediumIcons;
        var zoom = values[1] is double z ? z : 50.0;

        double baseWidth = mode switch
        {
            ExplorerViewMode.ExtraLargeIcons => 140.0,
            ExplorerViewMode.LargeIcons => 100.0,
            ExplorerViewMode.MediumIcons => 80.0,
            ExplorerViewMode.SmallIcons => 60.0,
            _ => 80.0
        };

        var factor = 0.5 + (zoom / 100.0) * 1.5;
        var width = Math.Clamp(baseWidth * factor, 50.0, 300.0);
        return width;
    }
}

/// <summary>
/// Multi-value converter for font size based on ViewMode and IconZoomLevel.
/// </summary>
public sealed class ZoomAwareFontSizeConverter : IMultiValueConverter
{
    public static readonly ZoomAwareFontSizeConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return 11.0;

        var mode = values[0] is ExplorerViewMode m ? m : ExplorerViewMode.MediumIcons;
        var zoom = values[1] is double z ? z : 50.0;

        double baseFontSize = mode switch
        {
            ExplorerViewMode.ExtraLargeIcons => 13.0,
            ExplorerViewMode.LargeIcons => 12.0,
            ExplorerViewMode.MediumIcons => 11.0,
            ExplorerViewMode.SmallIcons => 10.0,
            _ => 11.0
        };

        // Font scales less aggressively: 0 → 0.8x, 50 → 1.0x, 100 → 1.3x
        var factor = 0.8 + (zoom / 100.0) * 0.5;
        var size = Math.Clamp(baseFontSize * factor, 9.0, 18.0);
        return size;
    }
}

public sealed class ViewModeToFontSizeConverter : IValueConverter
{
    public static readonly ViewModeToFontSizeConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExplorerViewMode mode)
        {
            return mode switch
            {
                ExplorerViewMode.ExtraLargeIcons => 13.0,
                ExplorerViewMode.LargeIcons => 12.0,
                ExplorerViewMode.MediumIcons => 11.0,
                ExplorerViewMode.SmallIcons => 10.0,
                _ => 11.0
            };
        }
        return 11.0;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ViewModeToItemWidthConverter : IValueConverter
{
    public static readonly ViewModeToItemWidthConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExplorerViewMode mode)
        {
            return mode switch
            {
                ExplorerViewMode.ExtraLargeIcons => 140.0,
                ExplorerViewMode.LargeIcons => 100.0,
                ExplorerViewMode.MediumIcons => 80.0,
                ExplorerViewMode.SmallIcons => 60.0,
                _ => 80.0
            };
        }
        return 80.0;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ViewModeToUseThumbnailsConverter : IValueConverter
{
    public static readonly ViewModeToUseThumbnailsConverter Instance = new();       
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExplorerViewMode mode)
        {
            return mode is ExplorerViewMode.ExtraLargeIcons or ExplorerViewMode.LargeIcons or ExplorerViewMode.MediumIcons;
        }
        return false;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true if the file item has a ZIP extension (used to show/hide Extract menu item).
/// </summary>
public sealed class IsZipFileConverter : IValueConverter
{
    public static readonly IsZipFileConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path)
        {
            return path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true if the file item is a directory (used to conditionally show folder-only menu items).
/// </summary>
public sealed class IsDirectoryConverter : IValueConverter
{
    public static readonly IsDirectoryConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NexusExplorer.Core.Models.FileSystemItemType type)
            return type == NexusExplorer.Core.Models.FileSystemItemType.Directory;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when the FileSystemItemType is NOT a Drive (inverse of IsDriveTypeConverter).
/// </summary>
public sealed class IsNonDriveConverter : IValueConverter
{
    public static readonly IsNonDriveConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not NexusExplorer.Core.Models.FileSystemItemType.Drive;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true if the FileSystemItemType is Drive.
/// </summary>
public sealed class IsDriveTypeConverter : IValueConverter
{
    public static readonly IsDriveTypeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is NexusExplorer.Core.Models.FileSystemItemType.Drive;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Formats drive space info like "45.2 GB free of 237.9 GB".
/// </summary>
public sealed class DriveSpaceInfoConverter : IValueConverter
{
    public static readonly DriveSpaceInfoConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NexusExplorer.Core.Models.FileSystemItem item && item.TotalSpace.HasValue && item.FreeSpace.HasValue)
        {
            var free = FormatSize(item.FreeSpace.Value);
            var total = FormatSize(item.TotalSpace.Value);
            return $"{free} free of {total}";
        }
        return string.Empty;
    }

    private static string FormatSize(long bytes)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB" };
        if (bytes == 0) return "0 B";
        var place = System.Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return $"{num} {suf[place]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns a status brush for a drive's usage bar based on how full it is:
/// green/accent when there's plenty of room, amber when getting full, red when nearly full.
/// </summary>
public sealed class DriveUsageBrushConverter : IValueConverter
{
    public static readonly DriveUsageBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = value switch
        {
            NexusExplorer.Core.Models.FileSystemItem item => item.UsagePercent,
            double d => d,
            _ => 0
        };

        var hex = pct >= 90 ? "#E53935"   // red: critically full
                : pct >= 75 ? "#FB8C00"   // amber: getting full
                : "#43A047";              // green: healthy
        return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Short free-space label for a drive, e.g. "54.8 GB free".
/// </summary>
public sealed class DriveFreeTextConverter : IValueConverter
{
    public static readonly DriveFreeTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NexusExplorer.Core.Models.FileSystemItem item && item.FreeSpace.HasValue)
            return $"{FormatSize(item.FreeSpace.Value)} free";
        return string.Empty;
    }

    private static string FormatSize(long bytes)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB" };
        if (bytes == 0) return "0 B";
        var place = System.Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return $"{num} {suf[place]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true if the FileSystemItem is an image file (for enabling thumbnail in list/details views).
/// Checks file extension against common image formats.
/// </summary>
public sealed class IsImageFileConverter : IValueConverter
{
    public static readonly IsImageFileConverter Instance = new();

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".svg", ".tiff", ".tif"
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NexusExplorer.Core.Models.FileSystemItemType type)
            return type == NexusExplorer.Core.Models.FileSystemItemType.File;

        // If bound to the full item, check extension
        if (value is NexusExplorer.Core.Models.FileSystemItem item)
        {
            var ext = item.Extension ?? System.IO.Path.GetExtension(item.Name);
            return !string.IsNullOrEmpty(ext) && ImageExtensions.Contains(ext);
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a color hex string to a brush. An empty/null hex (the "Default" folder-color
/// menu entry) yields a transparent brush so the swatch renders as an empty ring.
/// </summary>
public sealed class ColorHexToBrushConverter : IValueConverter
{
    public static readonly ColorHexToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try { return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex)); }
            catch { /* fall through to transparent */ }
        }
        return Avalonia.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
