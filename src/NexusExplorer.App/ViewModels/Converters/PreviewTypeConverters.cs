using System.Globalization;
using Avalonia.Data.Converters;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Returns true when PreviewType is Image.
/// </summary>
public sealed class IsImagePreviewConverter : IValueConverter
{
    public static readonly IsImagePreviewConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PreviewType.Image;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when PreviewType is Text.
/// </summary>
public sealed class IsTextPreviewConverter : IValueConverter
{
    public static readonly IsTextPreviewConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PreviewType.Text;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when PreviewType is Folder.
/// </summary>
public sealed class IsFolderPreviewConverter : IValueConverter
{
    public static readonly IsFolderPreviewConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PreviewType.Folder;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when PreviewType is Unsupported.
/// </summary>
public sealed class IsUnsupportedPreviewConverter : IValueConverter
{
    public static readonly IsUnsupportedPreviewConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PreviewType.Unsupported;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when PreviewType is None (no selection).
/// </summary>
public sealed class IsNoPreviewConverter : IValueConverter
{
    public static readonly IsNoPreviewConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PreviewType.None;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when there IS something selected to preview (PreviewType != None).
/// </summary>
public sealed class HasSelectionPreviewConverter : IValueConverter
{
    public static readonly HasSelectionPreviewConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not PreviewType.None;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when there IS a selection but no image to render in the hero area
/// (Text, Folder or Unsupported) — i.e. the file icon should be shown instead.
/// </summary>
public sealed class IsIconPreviewConverter : IValueConverter
{
    public static readonly IsIconPreviewConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PreviewType.Text or PreviewType.Folder or PreviewType.Unsupported;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when the preview has inline body content to show (text file body
/// or folder listing).
/// </summary>
public sealed class HasPreviewBodyConverter : IValueConverter
{
    public static readonly HasPreviewBodyConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PreviewType.Text or PreviewType.Folder;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
