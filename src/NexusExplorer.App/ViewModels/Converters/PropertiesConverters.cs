using System.Globalization;
using Avalonia.Data.Converters;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Converts IsDirectory boolean to an icon character for the Properties dialog.
/// </summary>
public sealed class PropertiesIconConverter : IValueConverter
{
    public static readonly PropertiesIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return "\U0001F4C1"; // folder icon
        return "\U0001F4C4"; // file icon
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
