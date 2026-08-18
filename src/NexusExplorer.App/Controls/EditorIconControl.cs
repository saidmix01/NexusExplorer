using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentIcons.Avalonia.Fluent;
using FluentIcons.Common;
using NexusExplorer.App.Services.Thumbnails;

namespace NexusExplorer.App.Controls;

/// <summary>
/// Shows the icon of an installed editor, extracting the native executable icon
/// on Windows and falling back to a generic code symbol.
/// </summary>
public sealed class EditorIconControl : Panel
{
    public static readonly StyledProperty<string?> ExecutablePathProperty =
        AvaloniaProperty.Register<EditorIconControl, string?>(nameof(ExecutablePath));

    private readonly Image _image;
    private readonly SymbolIcon _symbol;
    private CancellationTokenSource? _cts;

    public EditorIconControl()
    {
        _image = new Image
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            IsVisible = false
        };
        _symbol = new SymbolIcon
        {
            Symbol = Symbol.Code,
            IconVariant = IconVariant.Filled,
            FontSize = 16,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        Children.Add(_image);
        Children.Add(_symbol);
    }

    public string? ExecutablePath
    {
        get => GetValue(ExecutablePathProperty);
        set => SetValue(ExecutablePathProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ExecutablePathProperty)
            UpdateIcon();
    }

    private async void UpdateIcon()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _image.Source = null;
        _image.IsVisible = false;
        _symbol.IsVisible = true;

        var path = ExecutablePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            var bitmap = await WindowsShellIconExtractor.ExtractIconAsync(path, 16, cts.Token);
            if (cts.IsCancellationRequested) return;

            if (bitmap is not null)
            {
                _image.Source = bitmap;
                _image.IsVisible = true;
                _symbol.IsVisible = false;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the editor changes rapidly.
        }
        catch
        {
            // Fall back to the generic symbol.
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
