using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Two-way converter for binding a radio button's IsChecked to an enum property.
/// Convert returns true when the bound enum value equals the ConverterParameter;
/// ConvertBack returns the parameter when the radio becomes checked (and does nothing
/// when it becomes unchecked, letting the newly-checked radio drive the value).
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Only push the parameter value back when this radio was checked.
        if (value is true && parameter is not null && targetType.IsEnum)
        {
            try { return Enum.Parse(targetType, parameter.ToString()!, ignoreCase: true); }
            catch { return BindingOperations.DoNothing; }
        }
        return BindingOperations.DoNothing;
    }
}
