using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FluentIcons.Avalonia.Fluent;
using FluentIcons.Common;
using Microsoft.Extensions.DependencyInjection;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Controls;

/// <summary>
/// Custom control that renders file/folder icons using FluentIcons SymbolIcon
/// with async thumbnail support. Folders use a custom blue Finder-style drawing.
/// Used by all file views (Icon, List, Details).
/// </summary>
public class FileIconControl : Panel
{
    public static readonly StyledProperty<FileSystemItem?> ItemProperty =
        AvaloniaProperty.Register<FileIconControl, FileSystemItem?>(nameof(Item));

    public static readonly StyledProperty<int> IconSizeProperty =
        AvaloniaProperty.Register<FileIconControl, int>(nameof(IconSize), 24);

    public static readonly StyledProperty<bool> UseThumbnailsProperty =
        AvaloniaProperty.Register<FileIconControl, bool>(nameof(UseThumbnails), false);

    /// <summary>
    /// When true, folders render as a flat filled Fluent folder glyph (Frame 2 card look)
    /// instead of the glossy macOS-style vector folder. Used by the icon (grid) view.
    /// </summary>
    public static readonly StyledProperty<bool> FlatStyleProperty =
        AvaloniaProperty.Register<FileIconControl, bool>(nameof(FlatStyle), false);

    private readonly SymbolIcon _symbolIcon;
    private readonly Image _thumbnailImage;
    private readonly Image _folderImage;
    private CancellationTokenSource? _cts;

    // Cached folder drawings at various sizes
    private static readonly Dictionary<int, DrawingImage> FolderDrawingCache = new();

    public FileIconControl()
    {
        _symbolIcon = new SymbolIcon
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            IconVariant = IconVariant.Filled
        };
        _thumbnailImage = new Image
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Stretch = Stretch.Uniform
        };
        _folderImage = new Image
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Stretch = Stretch.Uniform
        };

        Children.Add(_symbolIcon);
        Children.Add(_thumbnailImage);
        Children.Add(_folderImage);
    }

    public FileSystemItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public int IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public bool UseThumbnails
    {
        get => GetValue(UseThumbnailsProperty);
        set => SetValue(UseThumbnailsProperty, value);
    }

    public bool FlatStyle
    {
        get => GetValue(FlatStyleProperty);
        set => SetValue(FlatStyleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty || change.Property == IconSizeProperty
            || change.Property == UseThumbnailsProperty || change.Property == FlatStyleProperty)
        {
            UpdateIcon();
        }
    }

    private async void UpdateIcon()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        var item = Item;
        if (item == null)
        {
            _symbolIcon.IsVisible = false;
            _thumbnailImage.IsVisible = false;
            _folderImage.IsVisible = false;
            return;
        }

        // Apply IconSize
        _symbolIcon.FontSize = IconSize;
        _thumbnailImage.Width = IconSize;
        _thumbnailImage.Height = IconSize;
        _folderImage.Width = IconSize;
        _folderImage.Height = IconSize;

        // Apply reduced opacity for hidden files
        Opacity = item.IsHidden ? 0.55 : 1.0;

        // Hide all first. Also drop the previous thumbnail reference: the cache owns that
        // Bitmap and may dispose it on eviction/staleness, so a recycled control must not keep
        // rendering it (that would throw ObjectDisposedException during render).
        _symbolIcon.IsVisible = false;
        _thumbnailImage.IsVisible = false;
        _thumbnailImage.Source = null;
        _folderImage.IsVisible = false;

        // Folders get custom colored icon
        if (item.Type == FileSystemItemType.Directory)
        {
            // Check for custom folder color
            var folderColorService = App.Services.GetService<IFolderColorService>();
            var customColor = folderColorService?.GetColor(item.Path);

            if (FlatStyle)
            {
                // Flat filled folder glyph (Frame 2 card aesthetic): monochrome, centered.
                _symbolIcon.Symbol = Symbol.Folder;
                _symbolIcon.Foreground = new SolidColorBrush(
                    customColor is not null ? Color.Parse(customColor) : Color.Parse("#475569"));
                _symbolIcon.IsVisible = true;
                return;
            }

            _folderImage.Source = customColor is not null
                ? GetColoredFolderDrawing(IconSize, customColor)
                : GetFolderDrawing(IconSize);
            _folderImage.IsVisible = true;
            return;
        }

        // Drives get the symbol icon with a specific foreground
        if (item.Type == FileSystemItemType.Drive)
        {
            _symbolIcon.Foreground = new SolidColorBrush(Color.Parse("#607D8B"));
            _symbolIcon.Symbol = Symbol.HardDrive;
            _symbolIcon.IsVisible = true;
            return;
        }

        // Symbolic links / shortcuts — show link icon
        if (item.Type == FileSystemItemType.SymbolicLink)
        {
            _symbolIcon.Foreground = new SolidColorBrush(Color.Parse("#5C6BC0"));
            _symbolIcon.Symbol = Symbol.Link;
            _symbolIcon.IsVisible = true;
            return;
        }

        // Resolve icon using the centralized service
        var iconService = App.Services.GetService<IFileIconService>();
        var symbol = (Symbol)(iconService?.GetIcon(item) ?? Symbol.Document);
        _symbolIcon.Symbol = symbol;
        _symbolIcon.Foreground = GetIconForeground(symbol);
        _symbolIcon.IsVisible = true;

        // Attempt thumbnail loading if enabled
        // For shell-icon files (.lnk, .exe), always try extraction regardless of UseThumbnails
        var shouldTryThumbnail = UseThumbnails || IsShellIconFile(item);

        if (shouldTryThumbnail && App.Services.GetService<IThumbnailService>() is { } thumbnailService)
        {
            if (thumbnailService.CanGenerateThumbnail(item))
            {
                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                try
                {
                    var thumb = await thumbnailService.GetThumbnailAsync(item, IconSize, token);

                    if (!token.IsCancellationRequested && thumb is Bitmap bitmap)
                    {
                        _thumbnailImage.Source = bitmap;
                        _thumbnailImage.IsVisible = true;
                        _symbolIcon.IsVisible = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when item changes rapidly (e.g., scrolling)
                }
            }
        }
    }

    /// <summary>
    /// Returns true for file types that should always attempt shell icon extraction
    /// regardless of the UseThumbnails setting (shortcuts, executables).
    /// </summary>
    private static bool IsShellIconFile(FileSystemItem item)
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            return false;

        var ext = (item.Extension ?? System.IO.Path.GetExtension(item.Name))?.ToLowerInvariant();
        return ext is ".lnk" or ".url" or ".exe" or ".msi" or ".appref-ms";
    }

    /// <summary>
    /// Returns a color-coded foreground for different icon types to add visual variety
    /// while maintaining the Finder-inspired aesthetic.
    /// </summary>
    private static IBrush GetIconForeground(Symbol symbol)
    {
        return symbol switch
        {
            Symbol.Image => new SolidColorBrush(Color.Parse("#43A047")),       // Green for images
            Symbol.Video => new SolidColorBrush(Color.Parse("#7B1FA2")),       // Purple for video
            Symbol.MusicNote2 => new SolidColorBrush(Color.Parse("#E91E63")), // Pink for music
            Symbol.DocumentPdf => new SolidColorBrush(Color.Parse("#D32F2F")),// Red for PDF
            Symbol.Code => new SolidColorBrush(Color.Parse("#1565C0")),       // Blue for code
            Symbol.BracesVariable => new SolidColorBrush(Color.Parse("#F57C00")), // Orange for JSON
            Symbol.FolderZip => new SolidColorBrush(Color.Parse("#795548")),  // Brown for archives
            Symbol.Table => new SolidColorBrush(Color.Parse("#2E7D32")),      // Dark green for spreadsheets
            Symbol.AppGeneric => new SolidColorBrush(Color.Parse("#455A64")), // Blue-grey for executables
            Symbol.Database => new SolidColorBrush(Color.Parse("#00838F")),   // Teal for databases
            Symbol.WindowConsole => new SolidColorBrush(Color.Parse("#37474F")), // Dark for terminal/scripts
            Symbol.DocumentText => new SolidColorBrush(Color.Parse("#546E7A")), // Grey-blue for text docs
            Symbol.Settings => new SolidColorBrush(Color.Parse("#616161")),   // Grey for config
            Symbol.Certificate => new SolidColorBrush(Color.Parse("#FF8F00")),// Gold for certificates
            Symbol.BookOpen => new SolidColorBrush(Color.Parse("#4527A0")),   // Deep purple for readme
            Symbol.HardDrive => new SolidColorBrush(Color.Parse("#607D8B")), // Blue-grey for drives
            Symbol.Link => new SolidColorBrush(Color.Parse("#5C6BC0")),       // Indigo for shortcuts/links
            _ => new SolidColorBrush(Color.Parse("#616161"))                  // Default grey
        };
    }

    /// <summary>
    /// Creates a blue macOS Mavericks-style folder icon as a DrawingImage.
    /// The folder has a tab, rounded body, subtle gradient, and dimensional shading.
    /// </summary>
    private static DrawingImage GetFolderDrawing(int size)
    {
        // Round to nearest standard size for caching
        var cacheKey = size <= 16 ? 16 : size <= 32 ? 32 : size <= 48 ? 48 : size <= 64 ? 64 : 96;

        if (FolderDrawingCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var drawing = CreateFolderDrawing();
        FolderDrawingCache[cacheKey] = drawing;
        return drawing;
    }

    // Cache for colored folder drawings: key = "size_color"
    private static readonly Dictionary<string, DrawingImage> ColoredFolderCache = new();

    private static DrawingImage GetColoredFolderDrawing(int size, string colorHex)
    {
        var cacheKey = $"{size}_{colorHex}";
        if (ColoredFolderCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var drawing = CreateColoredFolderDrawing(colorHex);
        ColoredFolderCache[cacheKey] = drawing;
        return drawing;
    }

    private static DrawingImage CreateColoredFolderDrawing(string colorHex)
    {
        var baseColor = Color.Parse(colorHex);
        // Generate lighter and darker variants
        var lighter = LightenColor(baseColor, 0.3);
        var darker = DarkenColor(baseColor, 0.2);
        var darkest = DarkenColor(baseColor, 0.35);
        var tabLight = LightenColor(baseColor, 0.4);

        var group = new DrawingGroup();

        // Folder body
        var bodyGeo = new RectangleGeometry(new Rect(0, 4, 24, 17)) { RadiusX = 2, RadiusY = 2 };
        var bodyBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(lighter, 0),
                new GradientStop(baseColor, 0.3),
                new GradientStop(darker, 0.7),
                new GradientStop(darkest, 1.0)
            }
        };
        group.Children.Add(new GeometryDrawing
        {
            Geometry = bodyGeo,
            Brush = bodyBrush,
            Pen = new Pen(new SolidColorBrush(darkest), 0.5)
        });

        // Folder tab
        var tabGeo = new PathGeometry();
        var tabFigure = new PathFigure { StartPoint = new Point(1, 4), IsClosed = true, IsFilled = true };
        tabFigure.Segments!.Add(new LineSegment { Point = new Point(1, 2) });
        tabFigure.Segments.Add(new ArcSegment { Point = new Point(3, 0.5), Size = new Size(2, 2), IsLargeArc = false, SweepDirection = SweepDirection.Clockwise });
        tabFigure.Segments.Add(new LineSegment { Point = new Point(9, 0.5) });
        tabFigure.Segments.Add(new LineSegment { Point = new Point(11, 4) });
        tabGeo.Figures!.Add(tabFigure);

        var tabBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(tabLight, 0),
                new GradientStop(lighter, 1)
            }
        };
        group.Children.Add(new GeometryDrawing
        {
            Geometry = tabGeo,
            Brush = tabBrush,
            Pen = new Pen(new SolidColorBrush(darkest), 0.5)
        });

        // Highlight
        var highlightGeo = new RectangleGeometry(new Rect(0.5, 5, 23, 6)) { RadiusX = 1.5, RadiusY = 1.5 };
        var highlightBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#30FFFFFF"), 0),
                new GradientStop(Color.Parse("#00FFFFFF"), 1)
            }
        };
        group.Children.Add(new GeometryDrawing { Geometry = highlightGeo, Brush = highlightBrush });

        // Shadow
        var shadowGeo = new RectangleGeometry(new Rect(1, 19.5, 22, 1)) { RadiusX = 0.5, RadiusY = 0.5 };
        group.Children.Add(new GeometryDrawing { Geometry = shadowGeo, Brush = new SolidColorBrush(Color.Parse("#15000000")) });

        return new DrawingImage(group);
    }

    private static Color LightenColor(Color c, double amount)
    {
        var r = (byte)Math.Min(255, c.R + (255 - c.R) * amount);
        var g = (byte)Math.Min(255, c.G + (255 - c.G) * amount);
        var b = (byte)Math.Min(255, c.B + (255 - c.B) * amount);
        return Color.FromArgb(c.A, r, g, b);
    }

    private static Color DarkenColor(Color c, double amount)
    {
        var r = (byte)(c.R * (1 - amount));
        var g = (byte)(c.G * (1 - amount));
        var b = (byte)(c.B * (1 - amount));
        return Color.FromArgb(c.A, r, g, b);
    }

    private static DrawingImage CreateFolderDrawing()
    {
        var group = new DrawingGroup();

        // Folder body (main rectangle with rounded corners)
        var bodyGeo = new RectangleGeometry(new Rect(0, 4, 24, 17))
        {
            RadiusX = 2,
            RadiusY = 2
        };
        var bodyBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#7CBAED"), 0),    // Light blue top
                new GradientStop(Color.Parse("#4A9FDE"), 0.3),  // Mid blue  
                new GradientStop(Color.Parse("#3B8DD4"), 0.7),  // Darker blue
                new GradientStop(Color.Parse("#2E7BC9"), 1.0)   // Bottom blue
            }
        };
        group.Children.Add(new GeometryDrawing
        {
            Geometry = bodyGeo,
            Brush = bodyBrush,
            Pen = new Pen(new SolidColorBrush(Color.Parse("#2670B8")), 0.5)
        });

        // Folder tab (top-left flap)
        var tabGeo = new PathGeometry();
        var tabFigure = new PathFigure { StartPoint = new Point(1, 4), IsClosed = true, IsFilled = true };
        tabFigure.Segments!.Add(new LineSegment { Point = new Point(1, 2) });
        tabFigure.Segments.Add(new ArcSegment { Point = new Point(3, 0.5), Size = new Size(2, 2), IsLargeArc = false, SweepDirection = SweepDirection.Clockwise });
        tabFigure.Segments.Add(new LineSegment { Point = new Point(9, 0.5) });
        tabFigure.Segments.Add(new LineSegment { Point = new Point(11, 4) });
        tabGeo.Figures!.Add(tabFigure);

        var tabBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#8ECAF2"), 0),
                new GradientStop(Color.Parse("#5AABEA"), 1)
            }
        };
        group.Children.Add(new GeometryDrawing
        {
            Geometry = tabGeo,
            Brush = tabBrush,
            Pen = new Pen(new SolidColorBrush(Color.Parse("#2670B8")), 0.5)
        });

        // Front face overlay (slight highlight at top of body for 3D effect)
        var highlightGeo = new RectangleGeometry(new Rect(0.5, 5, 23, 6))
        {
            RadiusX = 1.5,
            RadiusY = 1.5
        };
        var highlightBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#30FFFFFF"), 0),
                new GradientStop(Color.Parse("#00FFFFFF"), 1)
            }
        };
        group.Children.Add(new GeometryDrawing
        {
            Geometry = highlightGeo,
            Brush = highlightBrush
        });

        // Bottom shadow line for depth
        var shadowGeo = new RectangleGeometry(new Rect(1, 19.5, 22, 1))
        {
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        group.Children.Add(new GeometryDrawing
        {
            Geometry = shadowGeo,
            Brush = new SolidColorBrush(Color.Parse("#15000000"))
        });

        return new DrawingImage(group);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
