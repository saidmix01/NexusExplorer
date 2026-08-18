using System.Globalization;
using Avalonia.Data.Converters;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

public sealed class IsExplorerOnlyConverter : IValueConverter
{
    public static readonly IsExplorerOnlyConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LayoutMode.ExplorerOnly;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IsTerminalOnlyConverter : IValueConverter
{
    public static readonly IsTerminalOnlyConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LayoutMode.TerminalOnly;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IsSplitModeConverter : IValueConverter
{
    public static readonly IsSplitModeConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LayoutMode.Split;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IsHorizontalSplitConverter : IValueConverter
{
    public static readonly IsHorizontalSplitConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is SplitOrientation.Horizontal;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
