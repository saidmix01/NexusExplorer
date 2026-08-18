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
/// A floating preview control that shows during drag & drop.
/// Displays the dragged item's icon/thumbnail with name and count badge.
/// Must be placed in the MainWindow with IsHitTestVisible=False.
/// </summary>
public partial class DragPreviewControl : UserControl
{
    private Image? _thumbnailImage;
    private Panel? _iconPanel;
    private TextBlock? _fileNameText;
    private Border? _countBadge;
    private TextBlock? _countText;
    private Border? _previewBorder;

    public DragPreviewControl()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _thumbnailImage = this.FindControl<Image>("ThumbnailImage");
        _iconPanel = this.FindControl<Panel>("IconPanel");
        _fileNameText = this.FindControl<TextBlock>("FileNameText");
        _countBadge = this.FindControl<Border>("CountBadge");
        _countText = this.FindControl<TextBlock>("CountText");
        _previewBorder = this.FindControl<Border>("PreviewBorder");
    }

    /// <summary>
    /// Shows the drag preview for the given items.
    /// Uses the first item for icon/thumbnail, shows count badge for multi-select.
    /// </summary>
    public void Show(IReadOnlyList<string> paths, FileSystemItem? primaryItem)
    {
        if (paths.Count == 0) return;

        IsVisible = true;
        Opacity = 0.82;

        // Set name
        if (_fileNameText is not null)
        {
            var name = System.IO.Path.GetFileName(paths[0]);
            _fileNameText.Text = paths.Count == 1 ? name : name;
        }

        // Set count badge
        if (_countBadge is not null && _countText is not null)
        {
            if (paths.Count > 1)
            {
                _countBadge.IsVisible = true;
                _countText.Text = $"+{paths.Count}";
            }
            else
            {
                _countBadge.IsVisible = false;
            }
        }

        // Set icon/thumbnail
        SetIcon(primaryItem);
    }

    /// <summary>
    /// Updates the position of the preview to follow the cursor.
    /// </summary>
    public void UpdatePosition(Point position)
    {
        // Offset so preview appears slightly below and right of cursor
        RenderTransform = new TranslateTransform(position.X + 12, position.Y + 8);
    }

    /// <summary>
    /// Updates visual state based on whether the current target is valid.
    /// </summary>
    public void SetValidTarget(bool isValid)
    {
        Opacity = isValid ? 0.85 : 0.55;

        if (_previewBorder is not null)
        {
            _previewBorder.BorderBrush = isValid
                ? null
                : new SolidColorBrush(Color.Parse("#CC4444"));
            _previewBorder.BorderThickness = isValid
                ? new Thickness(0)
                : new Thickness(1.5);
        }
    }

    /// <summary>
    /// Hides the drag preview.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
        if (_thumbnailImage is not null)
        {
            _thumbnailImage.Source = null;
            _thumbnailImage.IsVisible = false;
        }
        if (_iconPanel is not null)
        {
            _iconPanel.Children.Clear();
            _iconPanel.IsVisible = true;
        }
    }

    private void SetIcon(FileSystemItem? item)
    {
        if (_thumbnailImage is null || _iconPanel is null) return;

        _thumbnailImage.IsVisible = false;
        _iconPanel.IsVisible = true;
        _iconPanel.Children.Clear();

        if (item is null)
        {
            // Fallback: generic document icon
            _iconPanel.Children.Add(CreateSymbolIcon(Symbol.Document, "#616161"));
            return;
        }

        // Folders: use blue folder icon (same as FileIconControl)
        if (item.Type == FileSystemItemType.Directory)
        {
            var folderIcon = new FileIconControl
            {
                Item = item,
                IconSize = 48,
                UseThumbnails = false,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            _iconPanel.Children.Add(folderIcon);
            return;
        }

        // Try to get cached thumbnail
        var thumbnailService = App.Services.GetService<IThumbnailService>();
        if (thumbnailService is not null && thumbnailService.CanGenerateThumbnail(item))
        {
            // Try synchronous cache hit (don't block for async load during drag)
            _ = TryLoadThumbnailAsync(item, thumbnailService);
        }

        // Show icon immediately (will be replaced if thumbnail loads)
        var iconControl = new FileIconControl
        {
            Item = item,
            IconSize = 48,
            UseThumbnails = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        _iconPanel.Children.Add(iconControl);
    }

    private async System.Threading.Tasks.Task TryLoadThumbnailAsync(FileSystemItem item, IThumbnailService thumbnailService)
    {
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(200); // 200ms timeout
            var thumb = await thumbnailService.GetThumbnailAsync(item, 48, cts.Token);

            if (thumb is Bitmap bitmap && IsVisible && _thumbnailImage is not null)
            {
                _thumbnailImage.Source = bitmap;
                _thumbnailImage.IsVisible = true;
                if (_iconPanel is not null)
                    _iconPanel.IsVisible = false;
            }
        }
        catch
        {
            // Thumbnail not available quickly — keep showing icon
        }
    }

    private static SymbolIcon CreateSymbolIcon(Symbol symbol, string color)
    {
        return new SymbolIcon
        {
            Symbol = symbol,
            FontSize = 36,
            Foreground = new SolidColorBrush(Color.Parse(color)),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
    }
}
