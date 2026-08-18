using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using NexusExplorer.App.ViewModels;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Views;

public partial class FileView : UserControl
{
    private bool _isDragging;
    private Point _dragStart;
    private Border? _selectionRect;
    private Canvas? _selectionCanvas;

    // Minimum distance before starting rubber-band (avoids accidental drags)
    private const double DragThreshold = 4;
    private bool _dragThresholdMet;

    public FileView()
    {
        InitializeComponent();
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _selectionCanvas = this.FindControl<Canvas>("SelectionCanvas");
        _selectionRect = this.FindControl<Border>("SelectionRect");
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;

        // Only start rubber-band if clicked on empty space (not on an item)
        var hitControl = this.InputHitTest(e.GetPosition(this)) as Visual;
        if (IsItemHit(hitControl)) return;

        _dragStart = e.GetPosition(this);
        _isDragging = true;
        _dragThresholdMet = false;
        e.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _selectionRect is null || _selectionCanvas is null) return;

        var currentPos = e.GetPosition(this);

        // Check threshold before starting visual rubber-band
        if (!_dragThresholdMet)
        {
            var dist = Math.Sqrt(Math.Pow(currentPos.X - _dragStart.X, 2) + Math.Pow(currentPos.Y - _dragStart.Y, 2));
            if (dist < DragThreshold) return;
            _dragThresholdMet = true;
            _selectionRect.IsVisible = true;
            _selectionCanvas.IsHitTestVisible = false; // Keep it non-interactive
        }

        // Calculate rectangle bounds
        var x = Math.Min(_dragStart.X, currentPos.X);
        var y = Math.Min(_dragStart.Y, currentPos.Y);
        var w = Math.Abs(currentPos.X - _dragStart.X);
        var h = Math.Abs(currentPos.Y - _dragStart.Y);

        Canvas.SetLeft(_selectionRect, x);
        Canvas.SetTop(_selectionRect, y);
        _selectionRect.Width = w;
        _selectionRect.Height = h;

        // Select items within the rectangle
        var selectionBounds = new Rect(x, y, w, h);
        SelectItemsInRect(selectionBounds);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        e.Pointer.Capture(null);

        // If the drag threshold was never met, it was just a click on empty space — clear selection
        if (!_dragThresholdMet)
        {
            var listBox = FindActiveListBox();
            if (listBox is not null)
            {
                listBox.SelectedItems?.Clear();
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.SelectedItems.Clear();
                    vm.SelectedItem = null;
                }
            }
        }

        if (_selectionRect is not null)
            _selectionRect.IsVisible = false;
    }

    private void SelectItemsInRect(Rect selectionBounds)
    {
        // Find the active ListBox
        var listBox = FindActiveListBox();
        if (listBox is null) return;

        listBox.SelectedItems?.Clear();

        // Iterate through the visible list box items
        var itemContainerGenerator = listBox.ItemContainerGenerator;
        foreach (var item in listBox.Items)
        {
            if (item is not FileSystemItem fsItem || fsItem.IsGroupHeader) continue;

            var container = listBox.ContainerFromItem(item) as Control;
            if (container is null || !container.IsVisible) continue;

            // Get the container's bounds relative to this FileView
            var topLeft = container.TranslatePoint(new Point(0, 0), this);
            var bottomRight = container.TranslatePoint(new Point(container.Bounds.Width, container.Bounds.Height), this);

            if (topLeft is null || bottomRight is null) continue;

            var itemRect = new Rect(topLeft.Value, bottomRight.Value);

            if (selectionBounds.Intersects(itemRect))
            {
                listBox.SelectedItems?.Add(item);
            }
        }
    }

    private ListBox? FindActiveListBox()
    {
        // Find the currently visible view and its ListBox
        var detailsView = this.FindControl<FileDetailsView>("") ?? this.GetVisualDescendants().OfType<FileDetailsView>().FirstOrDefault();
        if (detailsView?.IsVisible == true)
            return detailsView.FindControl<ListBox>("FileListBox");

        var listView = this.GetVisualDescendants().OfType<FileListView>().FirstOrDefault();
        if (listView?.IsVisible == true)
            return listView.FindControl<ListBox>("FileListBox");

        var iconView = this.GetVisualDescendants().OfType<FileIconView>().FirstOrDefault();
        if (iconView?.IsVisible == true)
            return iconView.FindControl<ListBox>("FileListBox");

        return null;
    }

    private static bool IsItemHit(Visual? visual)
    {
        // Walk up the visual tree to see if we hit a ListBoxItem
        var current = visual;
        while (current is not null)
        {
            if (current is ListBoxItem) return true;
            current = current.GetVisualParent();
        }
        return false;
    }
}
