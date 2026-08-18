using System.Globalization;
using Avalonia.Data.Converters;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Converts a FileSystemItemType to a simple text icon representation.
/// </summary>
public sealed class FileTypeToIconConverter : IValueConverter
{
    public static readonly FileTypeToIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileSystemItem item)
        {
            if (item.Type == FileSystemItemType.Directory) return "\U0001F4C1";
            if (item.Type == FileSystemItemType.Drive) return "\U0001F4BF";
            if (item.Type == FileSystemItemType.SymbolicLink) return "\U0001F517";

            var ext = System.IO.Path.GetExtension(item.Name)?.ToLowerInvariant();
            return ext switch
            {
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".svg" => "\U0001F5BC", // Image
                ".pdf" => "\U0001F4D4", // PDF
                ".txt" or ".md" or ".rtf" or ".log" => "\U0001F4C4", // Text
                ".cs" or ".js" or ".ts" or ".html" or ".css" or ".json" or ".xml" or ".py" or ".cpp" or ".c" or ".h" => "\U0001F4BB", // Code
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "\U0001F4E6", // Archive
                ".exe" or ".msi" or ".bat" or ".cmd" or ".sh" => "\U0001F5B1", // Executable
                _ => "\U0001F4C4" // Generic file
            };
        }

        if (value is FileSystemItemType type)
        {
            return type switch
            {
                FileSystemItemType.Directory => "\U0001F4C1",
                FileSystemItemType.File => "\U0001F4C4",
                FileSystemItemType.Drive => "\U0001F4BF",
                FileSystemItemType.SymbolicLink => "\U0001F517",
                _ => "\u2753"
            };
        }

        return "\u2753";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
