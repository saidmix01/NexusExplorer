using System.Globalization;
using Avalonia.Data.Converters;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Converts a nullable long (bytes) to a human-readable file size string for the preview pane.
/// </summary>
public sealed class PreviewSizeConverter : IValueConverter
{
    public static readonly PreviewSizeConverter Instance = new();

    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long size || size < 0)
            return string.Empty;

        if (size == 0)
            return "0 B";

        var unitIndex = 0;
        var displaySize = (double)size;

        while (displaySize >= 1024 && unitIndex < Units.Length - 1)
        {
            displaySize /= 1024;
            unitIndex++;
        }

        return $"{displaySize:0.##} {Units[unitIndex]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
