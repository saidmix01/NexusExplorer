using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Converts a file path string to a Bitmap for image preview display.
/// Returns null if the path is null/empty or the file cannot be loaded.
/// </summary>
public sealed class PathToBitmapConverter : IValueConverter
{
    public static readonly PathToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return null;

        try
        {
            // Skip SVG — Avalonia Bitmap doesn't handle SVG natively
            if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!File.Exists(path))
                return null;

            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
