using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace NexusExplorer.App.Terminal;

/// <summary>
/// Render-only terminal control. Draws the terminal grid with ANSI colors and cursor.
/// Input handling is done by the parent TerminalView (UserControl with Focusable=True).
/// </summary>
public sealed class TerminalControl : Control
{
    private RichTerminalBuffer? _buffer;
    private TerminalTheme _theme = TerminalTheme.LinuxDark;
    private readonly Typeface _typeface = new("Cascadia Mono, Consolas, DejaVu Sans Mono, Courier New");
    private double _cellWidth;
    private double _cellHeight;
    private const double FontSize = 14;
    private double _baselineOffset;

    // Cursor blink
    private bool _cursorVisible = true;
    private DispatcherTimer? _cursorTimer;
    private bool _showCursor;

    // Batching
    private bool _renderPending;

    public TerminalControl()
    {
        ClipToBounds = true;
        IsHitTestVisible = true;

        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _cursorTimer.Tick += (_, _) =>
        {
            _cursorVisible = !_cursorVisible;
            InvalidateVisual();
        };
    }

    public void SetBuffer(RichTerminalBuffer buffer)
    {
        _buffer = buffer;
        MeasureCellSize();
        InvalidateVisual();
    }

    public void SetTheme(TerminalTheme theme)
    {
        _theme = theme;
        InvalidateVisual();
    }

    public void ShowCursor(bool show)
    {
        _showCursor = show;
        if (show)
            _cursorTimer?.Start();
        else
            _cursorTimer?.Stop();
        _cursorVisible = true;
        InvalidateVisual();
    }

    public void NotifyOutputChanged()
    {
        if (!_renderPending)
        {
            _renderPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _renderPending = false;
                InvalidateVisual();
            }, DispatcherPriority.Render);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        MeasureCellSize();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _cursorTimer?.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        context.DrawRectangle(new SolidColorBrush(_theme.Background), null, new Rect(bounds.Size));

        if (_buffer is null || _cellWidth <= 0 || _cellHeight <= 0) return;

        var visibleRows = Math.Min((int)(bounds.Height / _cellHeight), _buffer.Rows);

        // Always show the rows around the cursor (auto-scroll to bottom).
        // Calculate which screen rows to display so the cursor row is always visible.
        var firstScreenRow = 0;
        if (_buffer.CursorRow >= visibleRows)
            firstScreenRow = _buffer.CursorRow - visibleRows + 1;

        for (var i = 0; i < visibleRows; i++)
        {
            var screenRow = firstScreenRow + i;
            if (screenRow >= _buffer.Rows) break;

            var rowCells = _buffer.GetScreenRow(screenRow);
            var y = i * _cellHeight;

            for (var col = 0; col < _buffer.Columns && col * _cellWidth < bounds.Width; col++)
            {
                var cell = rowCells[col];
                var x = col * _cellWidth;
                var cellRect = new Rect(x, y, _cellWidth, _cellHeight);

                var fg = GetForegroundColor(cell);
                var bg = GetBackgroundColor(cell);

                if (cell.Attributes.HasFlag(CellAttributes.Inverse))
                    (fg, bg) = (bg, fg);

                if (bg != _theme.Background)
                    context.DrawRectangle(new SolidColorBrush(bg), null, cellRect);

                var ch = cell.Character;
                if (ch > ' ')
                {
                    var weight = cell.Attributes.HasFlag(CellAttributes.Bold) ? FontWeight.Bold : FontWeight.Normal;
                    var style = cell.Attributes.HasFlag(CellAttributes.Italic) ? FontStyle.Italic : FontStyle.Normal;
                    var tf = new Typeface(_typeface.FontFamily, style, weight);

                    var formattedText = new FormattedText(
                        ch.ToString(),
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        tf,
                        FontSize,
                        new SolidColorBrush(fg));

                    context.DrawText(formattedText, new Point(x, y));
                }

                if (cell.Attributes.HasFlag(CellAttributes.Underline))
                {
                    var pen = new Pen(new SolidColorBrush(fg), 1);
                    context.DrawLine(pen, new Point(x, y + _cellHeight - 2), new Point(x + _cellWidth, y + _cellHeight - 2));
                }
            }
        }

        // Cursor
        if (_showCursor && _cursorVisible)
        {
            var cursorRow = _buffer.CursorRow;
            var cursorCol = _buffer.CursorCol;

            // Cursor visual position relative to the viewport
            var cursorVisualRow = cursorRow - firstScreenRow;

            if (cursorVisualRow >= 0 && cursorVisualRow < visibleRows)
            {
                var cx = cursorCol * _cellWidth;
                var cy = cursorVisualRow * _cellHeight;
                context.DrawRectangle(new SolidColorBrush(_theme.CursorColor), null, new Rect(cx, cy, 2, _cellHeight));
            }
        }
    }

    private Color GetForegroundColor(TerminalCell cell)
    {
        if (cell.ForegroundIndex == 255) return _theme.Foreground;
        if (cell.ForegroundIndex < 16)
        {
            var idx = (int)cell.ForegroundIndex;
            if (cell.Attributes.HasFlag(CellAttributes.Bold) && idx < 8) idx += 8;
            return _theme.AnsiColors[idx];
        }
        return _theme.Foreground;
    }

    private Color GetBackgroundColor(TerminalCell cell)
    {
        if (cell.BackgroundIndex == 255) return _theme.Background;
        if (cell.BackgroundIndex < 16) return _theme.AnsiColors[cell.BackgroundIndex];
        return _theme.Background;
    }

    private void MeasureCellSize()
    {
        var formattedText = new FormattedText(
            "M",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _typeface,
            FontSize,
            Brushes.White);

        _cellWidth = formattedText.Width;
        _cellHeight = formattedText.Height + 2;
        _baselineOffset = formattedText.Height;
    }

    /// <summary>
    /// The width of a single monospace character cell in pixels.
    /// </summary>
    public double CellWidth => _cellWidth;

    /// <summary>
    /// The height of a single monospace character cell in pixels.
    /// </summary>
    public double CellHeight => _cellHeight;

    /// <summary>
    /// Fired when the control's pixel size changes, providing the new calculated columns and rows.
    /// </summary>
    public event Action<int, int>? TerminalSizeChanged;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);

        if (_cellWidth > 0 && _cellHeight > 0)
        {
            var cols = TerminalSizeHelper.CalculateColumns(finalSize.Width, _cellWidth);
            var rows = TerminalSizeHelper.CalculateRows(finalSize.Height, _cellHeight);
            TerminalSizeChanged?.Invoke(cols, rows);
        }

        return result;
    }
}
