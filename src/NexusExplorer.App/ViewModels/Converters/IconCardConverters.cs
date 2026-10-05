using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Returns a soft, rounded "card" background brush for an icon tile (Frame 2 look):
/// pastel per well-known folder category, a neutral tint for other folders, and a
/// light gray for files. Colors are translucent so they read on light and dark themes.
/// </summary>
public sealed class IconCardBackgroundConverter : IValueConverter
{
    public static readonly IconCardBackgroundConverter Instance = new();

    // Pastel category colors from the UI/UX spec (Frame 2).
    private static readonly Color Docs = Color.Parse("#BAE6FD");       // blue
    private static readonly Color Music = Color.Parse("#FBCFE8");      // pink
    private static readonly Color Downloads = Color.Parse("#DDD6FE");  // purple
    private static readonly Color Pictures = Color.Parse("#FED7AA");   // orange
    private static readonly Color Videos = Color.Parse("#C7D2FE");     // indigo
    private static readonly Color GenericFolder = Color.Parse("#BFDBFE"); // soft blue
    private static readonly Color GenericFile = Color.Parse("#94A3B8");   // slate gray

    // Opacity applied so the tile tint is soft rather than saturated.
    private const byte CardAlpha = 0x4D; // ~30%

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FileSystemItem item)
            return new SolidColorBrush(Tint(GenericFile));

        if (item.Type == FileSystemItemType.Directory)
        {
            var name = item.Name.ToLowerInvariant();
            var color = name switch
            {
                "documents" or "docs" => Docs,
                "music" or "audio" => Music,
                "downloads" => Downloads,
                "pictures" or "images" or "photos" => Pictures,
                "videos" or "movies" => Videos,
                _ => GenericFolder
            };
            return new SolidColorBrush(Tint(color));
        }

        if (item.Type == FileSystemItemType.Drive)
            return new SolidColorBrush(Tint(GenericFolder));

        // Files: tint by broad category based on extension.
        var ext = (item.Extension ?? Path.GetExtension(item.Name))?.ToLowerInvariant();
        var fileColor = ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".svg" or ".webp" => Pictures,
            ".mp3" or ".wav" or ".flac" or ".m4a" or ".ogg" => Music,
            ".mp4" or ".mkv" or ".mov" or ".avi" or ".webm" => Videos,
            ".pdf" or ".doc" or ".docx" or ".txt" or ".md" or ".rtf" => Docs,
            _ => GenericFile
        };
        return new SolidColorBrush(Tint(fileColor));
    }

    private static Color Tint(Color c) => Color.FromArgb(CardAlpha, c.R, c.G, c.B);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
