using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

public class InlineRenameTextVisibilityConverter : IMultiValueConverter
{
    public static readonly InlineRenameTextVisibilityConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 3 && 
            values[0] is FileSystemItem currentItem &&
            values[1] is FileSystemItem selectedItem &&
            values[2] is bool isRenaming)
        {
            return !(isRenaming && currentItem == selectedItem);
        }

        return true;
    }
}