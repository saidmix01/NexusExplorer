using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Controls;

/// <summary>
/// A virtualizing panel that lays items out left-to-right, wrapping to a new row when the current
/// row is full (like a horizontal <see cref="WrapPanel"/>) but only realizing the containers that
/// fall inside the visible viewport (plus a small buffer). This gives large directories the same
/// scrolling cost regardless of item count.
///
/// Design assumptions (true for the file icon view):
/// - Regular tiles all share the same width/height, so columns-per-row and row height can be
///   computed once per layout from the first realized tile.
/// - A "group header" item (<see cref="FileSystemItem.IsGroupHeader"/>) spans the full width and
///   therefore occupies its own row, matching the old WrapPanel where headers forced a line break.
///
/// Containers are created/prepared/recycled through <see cref="ItemContainerGenerator"/> so the
/// owning <see cref="ListBox"/> keeps full control of selection and keyboard navigation.
/// </summary>
public class VirtualizingWrapPanel : VirtualizingPanel
{
    // Realized containers keyed by item index, plus a recycle pool keyed by recycle key.
    private readonly Dictionary<int, Control> _realized = new();
    private readonly Dictionary<object, Stack<Control>> _recyclePool = new();
    private readonly Dictionary<Control, object?> _recycleKeys = new();

    private Size _lastConstraint;
    private double _itemWidth;   // measured width of a regular tile
    private double _itemHeight;  // uniform row height for regular tiles (max seen, allows 2-line names)
    private int _columns = 1;
    private double _layoutWidth;
    private Rect _viewport;

    // Each slot describes where an item sits: its row index and column index.
    // Group headers get a row to themselves (column 0, full width).
    private readonly List<Slot> _slots = new();
    private readonly List<double> _rowOffsets = new(); // Y offset (top) of each row
    private readonly List<double> _rowHeights = new();
    private double _extentHeight;

    private struct Slot
    {
        public int Row;
        public int Column;
        public bool IsHeader;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var items = Items;
        if (items.Count == 0)
        {
            RecycleAll();
            return default;
        }

        _lastConstraint = availableSize;

        // Measure one regular tile to learn the uniform tile size, creating it if needed.
        EnsureItemMetrics(items, availableSize);

        _layoutWidth = double.IsInfinity(availableSize.Width) ? _itemWidth : availableSize.Width;
        _columns = Math.Max(1, (int)Math.Floor(_layoutWidth / Math.Max(1, _itemWidth)));

        BuildSlots(items, _layoutWidth);

        UpdateViewport();
        RealizeViewport(items, availableSize);

        return new Size(_layoutWidth, _extentHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var items = Items;
        foreach (var (index, container) in _realized)
        {
            if (index < 0 || index >= _slots.Count) continue;
            var slot = _slots[index];
            var y = _rowOffsets[slot.Row];
            if (slot.IsHeader)
            {
                container.Arrange(new Rect(0, y, finalSize.Width, _rowHeights[slot.Row]));
            }
            else
            {
                var x = slot.Column * _itemWidth;
                container.Arrange(new Rect(x, y, _itemWidth, _rowHeights[slot.Row]));
            }
        }
        return finalSize;
    }

    private void EnsureItemMetrics(IReadOnlyList<object?> items, Size availableSize)
    {
        if (_itemWidth > 0 && _itemHeight > 0)
            return;

        // Find the first non-header item to measure as the representative tile.
        var probeIndex = -1;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is FileSystemItem { IsGroupHeader: false })
            {
                probeIndex = i;
                break;
            }
        }
        if (probeIndex < 0) probeIndex = 0;

        var probe = GetOrCreateContainer(items, probeIndex);
        // Measure the width with no constraint to learn the tile's natural (fixed) width, which is
        // driven by Border Width="{Binding ComputedItemWidth}" in the template.
        probe.Measure(Size.Infinity);
        _itemWidth = Math.Max(1, probe.DesiredSize.Width);

        // Measure the height with the width constrained so the name TextBlock can wrap to 2 lines;
        // this gives the real height a worst-case (2-line) tile needs, not the 1-line probe height.
        probe.Measure(new Size(_itemWidth, double.PositiveInfinity));
        _itemHeight = Math.Max(1, probe.DesiredSize.Height);
    }

    private void BuildSlots(IReadOnlyList<object?> items, double width)
    {
        _slots.Clear();
        _rowOffsets.Clear();
        _rowHeights.Clear();

        var row = 0;
        var col = 0;
        double y = 0;

        void StartRow(double height)
        {
            _rowOffsets.Add(y);
            _rowHeights.Add(height);
            y += height;
        }

        for (var i = 0; i < items.Count; i++)
        {
            var isHeader = items[i] is FileSystemItem { IsGroupHeader: true };

            if (isHeader)
            {
                // Close any partially filled tile row, then give the header its own row.
                if (col > 0)
                    col = 0;
                row = _rowHeights.Count;
                _slots.Add(new Slot { Row = row, Column = 0, IsHeader = true });
                // Header height: measure lazily; use tile height as a reasonable minimum.
                StartRow(MeasureHeaderHeight(items, i, width));
            }
            else
            {
                if (col == 0)
                {
                    row = _rowHeights.Count;
                    StartRow(_itemHeight);
                }
                _slots.Add(new Slot { Row = row, Column = col, IsHeader = false });
                col++;
                if (col >= _columns)
                    col = 0;
            }
        }

        _extentHeight = y;
    }

    private double MeasureHeaderHeight(IReadOnlyList<object?> items, int index, double width)
    {
        // Headers are cheap; realize-measure once to get an accurate height.
        var c = GetOrCreateContainer(items, index);
        c.Measure(new Size(width, double.PositiveInfinity));
        return Math.Max(_itemHeight * 0.6, c.DesiredSize.Height);
    }

    private void UpdateViewport()
    {
        var scrollViewer = this.FindAncestorOfType<ScrollViewer>();
        if (scrollViewer is not null)
        {
            var offsetY = scrollViewer.Offset.Y;
            var height = scrollViewer.Viewport.Height;
            _viewport = new Rect(0, offsetY, scrollViewer.Viewport.Width, height);
        }
        else
        {
            _viewport = new Rect(0, 0, _lastConstraint.Width, _lastConstraint.Height);
        }
    }

    private void RealizeViewport(IReadOnlyList<object?> items, Size availableSize)
    {
        if (_slots.Count == 0)
        {
            RecycleAll();
            return;
        }

        // Buffer one viewport-height of rows above and below for smooth scrolling.
        var buffer = _viewport.Height;
        var top = _viewport.Y - buffer;
        var bottom = _viewport.Y + _viewport.Height + buffer;

        var firstIndex = int.MaxValue;
        var lastIndex = -1;

        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            var rowTop = _rowOffsets[slot.Row];
            var rowBottom = rowTop + _rowHeights[slot.Row];
            if (rowBottom < top || rowTop > bottom)
                continue;
            if (i < firstIndex) firstIndex = i;
            if (i > lastIndex) lastIndex = i;
        }

        // Recycle anything now outside the realized range.
        var toRecycle = new List<int>();
        foreach (var index in _realized.Keys)
        {
            if (index < firstIndex || index > lastIndex)
                toRecycle.Add(index);
        }
        foreach (var index in toRecycle)
            RecycleContainer(index);

        if (lastIndex < 0)
            return;

        // Measure tiles with the width fixed but height unconstrained, so names that wrap to two
        // lines report their real height instead of being clipped to the probe's height.
        var maxTileHeight = _itemHeight;
        for (var i = firstIndex; i <= lastIndex; i++)
        {
            var container = GetOrCreateContainer(items, i);
            var slot = _slots[i];
            if (slot.IsHeader)
            {
                container.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            }
            else
            {
                container.Measure(new Size(_itemWidth, double.PositiveInfinity));
                if (container.DesiredSize.Height > maxTileHeight)
                    maxTileHeight = container.DesiredSize.Height;
            }
        }

        // If a taller tile appeared, adopt it as the uniform row height and re-layout once so all
        // rows line up and nothing is cut off.
        if (maxTileHeight > _itemHeight + 0.5)
        {
            _itemHeight = maxTileHeight;
            BuildSlots(items, _layoutWidth);
            InvalidateMeasure();
        }
    }

    // ----- container lifecycle -----

    private Control GetOrCreateContainer(IReadOnlyList<object?> items, int index)
    {
        if (_realized.TryGetValue(index, out var existing))
            return existing;

        var generator = ItemContainerGenerator!;
        var item = items[index];

        Control container;
        if (generator.NeedsContainer(item, index, out var recycleKey))
        {
            container = GetRecycled(recycleKey) ?? generator.CreateContainer(item, index, recycleKey);
            _recycleKeys[container] = recycleKey;
            container.IsVisible = true;
            generator.PrepareItemContainer(container, item, index);
            if (!Children.Contains(container))
                AddInternalChild(container);
            generator.ItemContainerPrepared(container, item, index);
        }
        else
        {
            // Item is its own container.
            container = (Control)item!;
            if (!Children.Contains(container))
                AddInternalChild(container);
            generator.PrepareItemContainer(container, item, index);
            generator.ItemContainerPrepared(container, item, index);
            _recycleKeys[container] = null;
        }

        _realized[index] = container;
        return container;
    }

    private Control? GetRecycled(object? recycleKey)
    {
        if (recycleKey is not null
            && _recyclePool.TryGetValue(recycleKey, out var pool)
            && pool.Count > 0)
        {
            return pool.Pop();
        }
        return null;
    }

    private void RecycleContainer(int index)
    {
        if (!_realized.TryGetValue(index, out var container))
            return;
        _realized.Remove(index);

        var generator = ItemContainerGenerator;
        var recycleKey = _recycleKeys.TryGetValue(container, out var key) ? key : null;

        if (recycleKey is null)
        {
            // Own-container item: cannot be recycled, just hide.
            container.IsVisible = false;
            return;
        }

        generator?.ClearItemContainer(container);
        container.IsVisible = false;

        if (!_recyclePool.TryGetValue(recycleKey, out var pool))
        {
            pool = new Stack<Control>();
            _recyclePool[recycleKey] = pool;
        }
        pool.Push(container);
    }

    private void RecycleAll()
    {
        var indices = new List<int>(_realized.Keys);
        foreach (var index in indices)
            RecycleContainer(index);
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        // Simplest correct behavior: drop all realized/recycled containers and re-layout.
        // The ListBox view model batches population into a single Reset, so this runs once per load.
        foreach (var kvp in _realized)
        {
            var container = kvp.Value;
            if (_recycleKeys.TryGetValue(container, out var key) && key is not null)
                ItemContainerGenerator?.ClearItemContainer(container);
            RemoveInternalChild(container);
        }
        _realized.Clear();
        _recycleKeys.Clear();
        _recyclePool.Clear();
        _itemWidth = 0;
        _itemHeight = 0;
        InvalidateMeasure();
    }

    // ----- VirtualizingPanel abstract members -----

    protected override Control? ScrollIntoView(int index)
    {
        var items = Items;
        if (index < 0 || index >= items.Count)
            return null;

        // Ensure slots are up to date, then bring the row into view and realize the container.
        if (index < _slots.Count)
        {
            var slot = _slots[index];
            var rowTop = _rowOffsets.Count > slot.Row ? _rowOffsets[slot.Row] : 0;
            this.FindAncestorOfType<ScrollViewer>()?.SetCurrentValue(
                ScrollViewer.OffsetProperty, new Vector(0, rowTop));
        }
        return _realized.TryGetValue(index, out var c) ? c : null;
    }

    protected override Control? ContainerFromIndex(int index) =>
        _realized.TryGetValue(index, out var c) ? c : null;

    protected override int IndexFromContainer(Control container)
    {
        foreach (var kvp in _realized)
        {
            if (ReferenceEquals(kvp.Value, container))
                return kvp.Key;
        }
        return -1;
    }

    protected override IEnumerable<Control>? GetRealizedContainers() => _realized.Values;

    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        var count = Items.Count;
        if (count == 0)
            return null;

        var fromIndex = from is Control c ? IndexFromContainer(c) : -1;
        var toIndex = fromIndex;

        switch (direction)
        {
            case NavigationDirection.First:
                toIndex = 0;
                break;
            case NavigationDirection.Last:
                toIndex = count - 1;
                break;
            case NavigationDirection.Next:
                toIndex++;
                break;
            case NavigationDirection.Previous:
                toIndex--;
                break;
            case NavigationDirection.Left:
                toIndex--;
                break;
            case NavigationDirection.Right:
                toIndex++;
                break;
            case NavigationDirection.Up:
                toIndex = StepRow(fromIndex, -1);
                break;
            case NavigationDirection.Down:
                toIndex = StepRow(fromIndex, +1);
                break;
            default:
                return null;
        }

        if (toIndex < 0 || toIndex >= count)
            return null;

        ScrollIntoView(toIndex);
        return ContainerFromIndex(toIndex);
    }

    private int StepRow(int fromIndex, int direction)
    {
        if (fromIndex < 0 || fromIndex >= _slots.Count)
            return fromIndex;
        var slot = _slots[fromIndex];
        var targetRow = slot.Row + direction;
        if (targetRow < 0 || targetRow >= _rowHeights.Count)
            return fromIndex;

        // Find an item in the target row at the same (or nearest) column.
        var best = -1;
        for (var i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].Row != targetRow) continue;
            if (best < 0) best = i;
            if (_slots[i].Column == slot.Column) { best = i; break; }
            if (_slots[i].Column <= slot.Column) best = i;
        }
        return best < 0 ? fromIndex : best;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Re-measure when the scroll offset changes so we realize the newly visible rows.
        var scrollViewer = this.FindAncestorOfType<ScrollViewer>();
        if (scrollViewer is not null)
        {
            scrollViewer.PropertyChanged += (_, args) =>
            {
                if (args.Property == ScrollViewer.OffsetProperty)
                    InvalidateMeasure();
            };
        }
    }
}
