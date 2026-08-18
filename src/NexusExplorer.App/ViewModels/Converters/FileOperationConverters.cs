using System.Globalization;
using Avalonia.Data.Converters;
using FluentIcons.Common;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Maps a <see cref="FileOperationType"/> to a Fluent icon for the File Operation Center.
/// </summary>
public sealed class FileOperationTypeToIconConverter : IValueConverter
{
    public static readonly FileOperationTypeToIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            FileOperationType.Copy => Symbol.Copy,
            FileOperationType.Move => Symbol.ArrowRight,
            FileOperationType.Delete => Symbol.Delete,
            FileOperationType.Compress => Symbol.FolderZip,
            FileOperationType.Extract => Symbol.FolderOpen,
            _ => Symbol.Document
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

