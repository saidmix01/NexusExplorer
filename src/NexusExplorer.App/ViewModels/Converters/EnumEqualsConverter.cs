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

/// <summary>
/// Two-way converter for binding a selection control (e.g. a rail RadioButton) to an int index.
/// Convert returns true when the bound value equals the ConverterParameter; ConvertBack returns
/// the parameter (as int) when checked, and DoNothing when unchecked.
/// </summary>
public sealed class IntEqualsConverter : IValueConverter
{
    public static readonly IntEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return int.TryParse(value.ToString(), out var v)
            && int.TryParse(parameter.ToString(), out var p)
            && v == p;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null && int.TryParse(parameter.ToString(), out var p))
            return p;
        return BindingOperations.DoNothing;
    }
}
