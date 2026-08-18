using System.Globalization;
using Avalonia.Data.Converters;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Converts a file size in bytes to a human-readable string.
/// </summary>
public sealed class FileSizeConverter : IValueConverter
{
    public static readonly FileSizeConverter Instance = new();

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

        return $"{displaySize:0.#} {Units[unitIndex]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
