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
