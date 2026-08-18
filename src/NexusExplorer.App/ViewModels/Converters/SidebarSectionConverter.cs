using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Converts a NavigationSection enum to its display header text.
/// </summary>
public sealed class SectionToHeaderConverter : IValueConverter
{
    public static readonly SectionToHeaderConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            NavigationSection.Favorites => "FAVORITES",
            NavigationSection.Locations => "LOCATIONS",
            NavigationSection.Network => "NETWORK",
            _ => ""
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
