using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using NexusExplorer.App.Terminal;
using NexusExplorer.App.ViewModels;

namespace NexusExplorer.App.Views;

public partial class TerminalView : UserControl
{
    private TerminalViewModel? _vm;
    private TerminalControl? _renderControl;
    private CancellationTokenSource? _resizeCts;
    private int _lastCols;
    private int _lastRows;

    // Debounce interval for resize events (ms)
    private const int ResizeDebounceMs = 50;

    public TerminalView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Detach from old control
        if (_renderControl is not null)
            _renderControl.TerminalSizeChanged -= OnTerminalSizeChanged;

        _vm = DataContext as TerminalViewModel;
        _renderControl = this.FindControl<TerminalControl>("TerminalRenderControl");

        if (_vm is not null && _renderControl is not null)
        {
            _vm.AttachControl(_renderControl);
            _renderControl.TerminalSizeChanged += OnTerminalSizeChanged;
        }
    }

    private void OnTerminalSizeChanged(int cols, int rows)
    {
        // Skip if dimensions haven't actually changed
        if (cols == _lastCols && rows == _lastRows) return;
        _lastCols = cols;
        _lastRows = rows;

        // Debounce: cancel any pending resize and schedule a new one
        _resizeCts?.Cancel();
        _resizeCts?.Dispose();
        _resizeCts = new CancellationTokenSource();
        var ct = _resizeCts.Token;

        _ = DebounceResizeAsync(cols, rows, ct);
    }

    private async Task DebounceResizeAsync(int cols, int rows, CancellationToken ct)
    {
        try
        {
            await Task.Delay(ResizeDebounceMs, ct);
            if (ct.IsCancellationRequested) return;

            if (_vm is not null)
                await _vm.ResizeAsync(cols, rows);
        }
        catch (TaskCanceledException)
        {
            // Expected when a newer resize supersedes this one
        }
    }

    private void TerminalSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Focus the UserControl so it receives keyboard events
        this.Focus();
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        _renderControl?.ShowCursor(true);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _renderControl?.ShowCursor(false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_vm is null || !_vm.IsSessionActive)
        {
            base.OnKeyDown(e);
            return;
        }

        byte[]? data = null;

        // Ctrl combinations
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.C: data = "\x03"u8.ToArray(); break;
                case Key.D: data = "\x04"u8.ToArray(); break;
                case Key.Z: data = "\x1a"u8.ToArray(); break;
                case Key.A: data = "\x01"u8.ToArray(); break;
                case Key.V: _ = PasteAsync(); e.Handled = true; return;
                case Key.L: base.OnKeyDown(e); return; // bubble to window
                case Key.OemTilde: base.OnKeyDown(e); return; // bubble to window
            }

            if (data is not null) { _ = _vm.SendKeyAsync(data); e.Handled = true; return; }
            base.OnKeyDown(e);
            return;
        }

        // Special keys
        switch (e.Key)
        {
            case Key.Enter: data = "\r"u8.ToArray(); break;
            case Key.Back: data = "\x08"u8.ToArray(); break;
            case Key.Tab: data = "\t"u8.ToArray(); break;
            case Key.Escape: data = "\x1b"u8.ToArray(); break;
            case Key.Up: data = "\x1b[A"u8.ToArray(); break;
            case Key.Down: data = "\x1b[B"u8.ToArray(); break;
            case Key.Right: data = "\x1b[C"u8.ToArray(); break;
            case Key.Left: data = "\x1b[D"u8.ToArray(); break;
            case Key.Home: data = "\x1b[H"u8.ToArray(); break;
            case Key.End: data = "\x1b[F"u8.ToArray(); break;
            case Key.Delete: data = "\x1b[3~"u8.ToArray(); break;
        }

        if (data is not null) { _ = _vm.SendKeyAsync(data); e.Handled = true; return; }

        // Regular character — use KeySymbol
        var text = e.KeySymbol;
        if (!string.IsNullOrEmpty(text))
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && text.Length == 1 && char.IsLetter(text[0]))
                text = text.ToUpperInvariant();
            _ = _vm.SendInputAsync(text);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (_vm is not null && _vm.IsSessionActive && !string.IsNullOrEmpty(e.Text))
        {
            _ = _vm.SendInputAsync(e.Text);
            e.Handled = true;
            return;
        }
        base.OnTextInput(e);
    }

    private async Task PasteAsync()
    {
        if (_vm is null) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        var text = await clipboard.TryGetTextAsync();
        if (!string.IsNullOrEmpty(text))
            await _vm.SendInputAsync(text);
    }

    public void FocusTerminal()
    {
        this.Focus();
    }
}
