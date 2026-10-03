using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Converts a folder color hex string (e.g. "#E05555") into a brush.
/// Returns Transparent when the color is null, empty, or invalid.
/// </summary>
public sealed class FolderColorBrushConverter : IValueConverter
{
    public static readonly FolderColorBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ParseFolderColor(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    internal static IBrush ParseFolderColor(object? value)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try { return new SolidColorBrush(Color.Parse(hex)); }
            catch { /* fall through to transparent */ }
        }
        return Brushes.Transparent;
    }
}

/// <summary>
/// Returns true when a folder color hex string is present (non-null, non-empty).
/// Used to show/hide the color swatch in the details view.
/// </summary>
public sealed class HasFolderColorConverter : IValueConverter
{
    public static readonly HasFolderColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string hex && !string.IsNullOrEmpty(hex);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when the value is null or an empty string. Used to show the default sidebar
/// icon for ordinary items (those without a color swatch).
/// </summary>
public sealed class IsNullConverter : IValueConverter
{
    public static readonly IsNullConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null || (value is string s && string.IsNullOrEmpty(s));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns a tab border brush from the tab's FolderColor and IsActive state.
/// Folder color takes precedence; otherwise the active/inactive theme border is used.
/// Inputs: [0] FolderColor (string?), [1] IsActive (bool).
/// </summary>
public sealed class FolderColorTabBorderMultiConverter : IMultiValueConverter
{
    public static readonly FolderColorTabBorderMultiConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 1 && values[0] is string hex && !string.IsNullOrEmpty(hex))
            return FolderColorBrushConverter.ParseFolderColor(hex);

        var isActive = values.Count >= 2 && values[1] is true;
        var key = isActive ? "NexusTabActiveBorder" : "NexusTabInactiveBorder";
        var app = Application.Current!;
        if (app.TryGetResource(key, app.ActualThemeVariant, out var resource) && resource is IBrush brush)
            return brush;
        if (app.TryGetResource(key, null, out resource) && resource is IBrush brush2)
            return brush2;

        return isActive
            ? new SolidColorBrush(Color.Parse("#0078D4"))
            : new SolidColorBrush(Color.Parse("#DCDCDC"));
    }
}
