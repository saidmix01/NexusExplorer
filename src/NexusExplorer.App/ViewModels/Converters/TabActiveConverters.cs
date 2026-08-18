using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Returns a background brush for the active/inactive tab pill.
/// Active: glassmorphism-tinted dark surface (NexusTabActiveBackground).
/// Inactive: subtle dark pill (NexusTabInactiveBackground).
/// </summary>
public sealed class TabActiveBackgroundConverter : IValueConverter
{
    public static readonly TabActiveBackgroundConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "NexusTabActiveBackground" : "NexusTabInactiveBackground";
        var app = Application.Current!;
        // Try with ActualThemeVariant first, then fallback to null variant for merged dictionaries
        if (app.TryGetResource(key, app.ActualThemeVariant, out var resource) && resource is IBrush brush)
            return brush;
        if (app.TryGetResource(key, null, out resource) && resource is IBrush brush2)
            return brush2;
        // Final fallback: white for active, light gray for inactive
        return value is true
            ? new SolidColorBrush(Color.Parse("#FFFFFF"))
            : new SolidColorBrush(Color.Parse("#F0F0F0"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Returns a border brush for the active/inactive tab pill.
/// Active: accent color border (blue in light, cyan in dark).
/// Inactive: subtle border matching tab bar.
/// </summary>
public sealed class TabActiveBorderConverter : IValueConverter
{
    public static readonly TabActiveBorderConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "NexusTabActiveBorder" : "NexusTabInactiveBorder";
        var app = Application.Current!;
        // Try with ActualThemeVariant first, then fallback to null variant for merged dictionaries
        if (app.TryGetResource(key, app.ActualThemeVariant, out var resource) && resource is IBrush brush)
            return brush;
        if (app.TryGetResource(key, null, out resource) && resource is IBrush brush2)
            return brush2;
        // Final fallback: accent blue for active, subtle gray for inactive
        return value is true
            ? new SolidColorBrush(Color.Parse("#0078D4"))
            : new SolidColorBrush(Color.Parse("#DCDCDC"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
