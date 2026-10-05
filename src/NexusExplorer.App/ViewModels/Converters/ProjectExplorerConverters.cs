using System.Globalization;
using Avalonia.Data.Converters;
using FluentIcons.Common;
using NexusExplorer.Core.Models;
using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.App.ViewModels;

/// <summary>True when the bound value is not <c>null</c> (used to show the script command preview).</summary>
public sealed class IsNotNullConverter : IValueConverter
{
    public static readonly IsNotNullConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True when the explorer content mode is <see cref="ExplorerContentMode.Files"/>.</summary>
public sealed class IsFilesContentModeConverter : IValueConverter
{
    public static readonly IsFilesContentModeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExplorerContentMode.Files;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns a highlight brush when the current <see cref="ExplorerContentMode"/> matches the
/// segment named by the converter parameter ("Files" or "Project"); otherwise transparent.
/// Used to show which segment of the Files/Project toggle is active.
/// </summary>
public sealed class ContentModeActiveBackgroundConverter : IValueConverter
{
    public static readonly ContentModeActiveBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ExplorerContentMode mode || parameter is not string segment)
            return Avalonia.Media.Brushes.Transparent;

        var isActive = segment switch
        {
            "Files" => mode == ExplorerContentMode.Files,
            "Project" => mode == ExplorerContentMode.Project,
            _ => false
        };

        if (!isActive)
            return Avalonia.Media.Brushes.Transparent;

        // Reuse the app's selection brush so the active segment matches the theme (Dark/Light).
        if (Avalonia.Application.Current?.TryGetResource(
                "NexusSelection", Avalonia.Application.Current.ActualThemeVariant, out var brush) == true
            && brush is Avalonia.Media.IBrush themed)
        {
            return themed;
        }

        return Avalonia.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True when the explorer content mode is <see cref="ExplorerContentMode.Project"/>.</summary>
public sealed class IsProjectContentModeConverter : IValueConverter
{
    public static readonly IsProjectContentModeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExplorerContentMode.Project;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a <see cref="ProjectType"/> to a short display label (e.g. "Node.js", ".NET").</summary>
public sealed class ProjectTypeNameConverter : IValueConverter
{
    public static readonly ProjectTypeNameConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ProjectType type ? DisplayName(type) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public static string DisplayName(ProjectType type) => type switch
    {
        ProjectType.Node => "Node.js",
        ProjectType.DotNet => ".NET",
        ProjectType.Rust => "Rust",
        ProjectType.Python => "Python",
        _ => "Unknown"
    };
}

/// <summary>Joins the detected technologies into a readable, comma-separated string.</summary>
public sealed class DetectedTechnologiesConverter : IValueConverter
{
    public static readonly DetectedTechnologiesConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable<ProjectType> types)
            return string.Join(", ", types.Select(ProjectTypeNameConverter.DisplayName));
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a <see cref="ProjectSectionKind"/> to a Fluent icon symbol for the section header.</summary>
public sealed class ProjectSectionIconConverter : IValueConverter
{
    public static readonly ProjectSectionIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ProjectSectionKind kind ? IconFor(kind) : Symbol.Folder;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Symbol IconFor(ProjectSectionKind kind) => kind switch
    {
        ProjectSectionKind.Project => Symbol.Box,
        ProjectSectionKind.Source => Symbol.Code,
        ProjectSectionKind.Tests => Symbol.Beaker,
        ProjectSectionKind.Assets => Symbol.Image,
        ProjectSectionKind.Configuration => Symbol.Settings,
        ProjectSectionKind.Documentation => Symbol.BookOpen,
        ProjectSectionKind.Scripts => Symbol.Flash,
        ProjectSectionKind.Development => Symbol.BranchFork,
        _ => Symbol.Folder
    };
}

/// <summary>
/// Maps a <see cref="ProjectCommand"/> to a Fluent icon symbol based on its label, so the
/// context-menu "Project" submenu shows a run/build/test-appropriate glyph.
/// </summary>
public sealed class ProjectCommandIconConverter : IValueConverter
{
    public static readonly ProjectCommandIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProjectCommand command)
            return Symbol.Play;

        var label = command.Label.ToLowerInvariant();
        return label switch
        {
            _ when label.Contains("build") => Symbol.Wrench,
            _ when label.Contains("test") => Symbol.Beaker,
            _ when label.Contains("install") || label.Contains("restore") => Symbol.ArrowDownload,
            _ when label.Contains("fmt") || label.Contains("format") || label.Contains("lint") => Symbol.Broom,
            _ when label.Contains("check") => Symbol.CheckmarkCircle,
            _ => Symbol.Play
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a <see cref="ProjectEntryKind"/> to a Fluent icon symbol for the entry row.</summary>
public sealed class ProjectEntryIconConverter : IValueConverter
{
    public static readonly ProjectEntryIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ProjectEntryKind kind ? IconFor(kind) : Symbol.Document;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Symbol IconFor(ProjectEntryKind kind) => kind switch
    {
        ProjectEntryKind.Directory => Symbol.Folder,
        ProjectEntryKind.File => Symbol.Document,
        ProjectEntryKind.Command => Symbol.Play,
        ProjectEntryKind.Information => Symbol.Info,
        _ => Symbol.Document
    };
}
