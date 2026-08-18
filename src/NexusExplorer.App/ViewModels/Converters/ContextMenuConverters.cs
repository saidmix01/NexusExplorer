using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Returns true if the item is a navigable container (folder or drive) — for "Open in New Tab", "Open in Terminal".
/// </summary>
public sealed class IsNavigableConverter : IValueConverter
{
    public static readonly IsNavigableConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileSystemItemType type)
            return type is FileSystemItemType.Directory or FileSystemItemType.Drive;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true if the item is a regular file (not folder, not drive) — for "Open With...".
/// </summary>
public sealed class IsFileConverter : IValueConverter
{
    public static readonly IsFileConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileSystemItemType type)
            return type is FileSystemItemType.File or FileSystemItemType.SymbolicLink;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true if the ViewMode is an icon mode (not List or Details) — for showing the zoom slider.
/// </summary>
public sealed class IsIconViewModeConverter : IValueConverter
{
    public static readonly IsIconViewModeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExplorerViewMode mode)
            return mode is not (ExplorerViewMode.List or ExplorerViewMode.Details);
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
