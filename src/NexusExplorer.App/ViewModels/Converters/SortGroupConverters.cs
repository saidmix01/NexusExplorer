using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

public class SortModeEqualityConverter : IValueConverter
{
    public static readonly SortModeEqualityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileSortMode mode && parameter is FileSortMode targetMode)
            return mode == targetMode;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SortDirectionEqualityConverter : IValueConverter
{
    public static readonly SortDirectionEqualityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SortDirection mode && parameter is SortDirection targetMode)
            return mode == targetMode;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class GroupModeEqualityConverter : IValueConverter
{
    public static readonly GroupModeEqualityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileGroupMode mode && parameter is FileGroupMode targetMode)
            return mode == targetMode;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
