using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FluentIcons.Common;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Converts a NavigationItemKind to a FluentIcons Symbol for the sidebar.
/// </summary>
public sealed class NavigationKindToIconConverter : IValueConverter
{
    public static readonly NavigationKindToIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            NavigationItemKind.QuickAccess => Symbol.Folder,
            NavigationItemKind.Drive => Symbol.HardDrive,
            NavigationItemKind.Favorite => Symbol.Star,
            NavigationItemKind.Network => Symbol.Globe,
            _ => Symbol.Folder
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a NavigationItem Name to a more specific FluentIcons Symbol
/// for well-known sidebar locations (Home, Desktop, Documents, etc.)
/// </summary>
public sealed class NavigationNameToIconConverter : IValueConverter
{
    public static readonly NavigationNameToIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name)
            return Symbol.Folder;

        return name.ToLowerInvariant() switch
        {
            "home" => Symbol.Home,
            "desktop" => Symbol.Desktop,
            "documents" => Symbol.Document,
            "downloads" => Symbol.ArrowDownload,
            "pictures" or "images" or "photos" => Symbol.Image,
            "videos" or "movies" => Symbol.Video,
            "music" or "audio" => Symbol.MusicNote2,
            "locations" or "location" => Symbol.Location,
            "this pc" => Symbol.Desktop,
            "recycle bin" or "trash" => Symbol.Delete,
            "network" or "networks" => Symbol.Globe,
            _ when name.Contains(":\\") || name.Contains(":/") => Symbol.HardDrive,
            _ => Symbol.Folder
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Returns a color brush per sidebar item name for colorful icons like macOS Finder.
/// </summary>
public sealed class NavigationNameToColorConverter : IValueConverter
{
    public static readonly NavigationNameToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name)
            return new SolidColorBrush(Color.Parse("#5A9FDE"));

        return new SolidColorBrush(name.ToLowerInvariant() switch
        {
            "home" => Color.Parse("#5A9FDE"),       // Blue
            "desktop" => Color.Parse("#6B7B8D"),    // Slate
            "documents" => Color.Parse("#4AADE8"),  // Light blue
            "downloads" => Color.Parse("#4AADE8"),  // Light blue
            "pictures" or "images" or "photos" => Color.Parse("#E8584A"), // Red-pink
            "videos" or "movies" => Color.Parse("#9B59B6"), // Purple
            "music" or "audio" => Color.Parse("#E84A6F"),   // Pink-red
            "this pc" => Color.Parse("#607D8B"),    // Blue-grey
            "recycle bin" or "trash" => Color.Parse("#78909C"), // Grey
            "network" => Color.Parse("#26A69A"),    // Teal
            _ when name.Contains(":\\") || name.Contains(":/") => Color.Parse("#78909C"), // Grey-blue
            _ => Color.Parse("#5A9FDE")             // Default blue
        });
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Returns true if the NavigationItemKind is Favorite (user-pinned item).
/// Used for showing/hiding the "Unpin" context menu item.
/// </summary>
public sealed class IsFavoriteKindConverter : IValueConverter
{
    public static readonly IsFavoriteKindConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is NavigationItemKind.Favorite;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Returns true if the NavigationItemKind is Custom (an item inside a user-created group).
/// Used for showing/hiding the "Remove from Group" context menu item.
/// </summary>
public sealed class IsCustomItemConverter : IValueConverter
{
    public static readonly IsCustomItemConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is NavigationItemKind.Custom;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a group's IsExpanded flag to a collapse/expand indicator glyph.
/// </summary>
public sealed class IsExpandedToGlyphConverter : IValueConverter
{
    public static readonly IsExpandedToGlyphConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "\u25BE" : "\u25B8"; // ▾ expanded, ▸ collapsed
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a NavigationItem's IsAvailable flag to an opacity value so that
/// items pointing to a missing path appear dimmed.
/// </summary>
public sealed class IsAvailableToOpacityConverter : IValueConverter
{
    public static readonly IsAvailableToOpacityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is false ? 0.55 : 1.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
