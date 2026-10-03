#pragma warning disable CS0618 // Suppress obsolete warnings for Avalonia DnD API transition (DataObject→DataTransfer)

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NexusExplorer.App.ViewModels;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Behaviors;

/// <summary>
/// Attached behavior that adds Drag & Drop support to file view controls.
/// Handles drag initiation (with movement threshold), drop validation, and visual feedback.
/// </summary>
public static class DragDropBehavior
{
    // Custom format identifier for internal Nexus drags
    private const string NexusFormatId = "nexus-explorer-paths";

    private static Point? _dragStartPoint;
    private static PointerPressedEventArgs? _dragStartArgs;
    private static bool _isDragging;
    private const double DragThreshold = 6.0;

    /// <summary>
    /// Attached property to enable drop target on a control.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(DragDropBehavior));

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    static DragDropBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            DragDrop.SetAllowDrop(control, true);
            control.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            control.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            control.AddHandler(DragDrop.DropEvent, OnDrop);
        }
        else
        {
            DragDrop.SetAllowDrop(control, false);
            control.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            control.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            control.RemoveHandler(DragDrop.DropEvent, OnDrop);
        }
    }

    // ================================================================
    // Drag initiation
    // ================================================================

    public static void BeginDragTracking(PointerPressedEventArgs e, Control source)
    {
        if (e.GetCurrentPoint(source).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(source);
            _dragStartArgs = e;
            _isDragging = false;
        }
    }

    public static bool ShouldStartDrag(PointerEventArgs e, Control source)
    {
        if (_dragStartPoint is null || _isDragging)
            return false;

        var currentPos = e.GetPosition(source);
        var diff = currentPos - _dragStartPoint.Value;

        if (Math.Abs(diff.X) > DragThreshold || Math.Abs(diff.Y) > DragThreshold)
        {
            _isDragging = true;
            _dragStartPoint = null;
            return true;
        }

        return false;
    }

    public static async void StartDrag(Control source, IReadOnlyList<string> paths, string sourceDirectory)
    {
        if (paths.Count == 0 || _dragStartArgs is null) return;

        var dataObject = new DataObject();

        // Internal Nexus format
        var internalPayload = string.Join("\n", paths) + "\n|SOURCE|" + sourceDirectory;
        dataObject.Set(NexusFormatId, internalPayload);

        // OS file format for external app interop (Windows Explorer, etc.)
        var storageProvider = TopLevel.GetTopLevel(source)?.StorageProvider;
        if (storageProvider is not null)
        {
            var items = new List<IStorageItem>();
            foreach (var path in paths)
            {
                try
                {
                    // Build the file URI from the absolute path directly. This correctly handles
                    // UNC paths (\\server\share) and escapes special characters (#, %, spaces),
                    // unlike manual "file:///" string concatenation.
                    var uri = new Uri(path);
                    if (System.IO.Directory.Exists(path))
                    {
                        var f = await storageProvider.TryGetFolderFromPathAsync(uri);
                        if (f is not null) items.Add(f);
                    }
                    else if (System.IO.File.Exists(path))
                    {
                        var f = await storageProvider.TryGetFileFromPathAsync(uri);
                        if (f is not null) items.Add(f);
                    }
                }
                catch { }
            }
            if (items.Count > 0)
                dataObject.Set(DataFormats.Files, items);
        }

        var effects = DragDropEffects.Move | DragDropEffects.Copy;
        await DragDrop.DoDragDrop(_dragStartArgs, dataObject, effects);

        _isDragging = false;
        _dragStartPoint = null;
        _dragStartArgs = null;

        // Notify that drag ended (for hiding preview)
        DragEnded?.Invoke();
    }

    /// <summary>
    /// Fired when a drag operation ends (drop completed, cancelled, or left the app).
    /// </summary>
    public static event Action? DragEnded;

    public static void EndDragTracking()
    {
        _dragStartPoint = null;
        _dragStartArgs = null;
        _isDragging = false;
    }

    // ================================================================
    // Drop target event handlers
    // ================================================================

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        var (paths, isInternal, sourceDir) = ExtractDragData(e);
        if (paths.Count == 0)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var destinationPath = GetDropDestination(sender, e);
        var validation = DragDropValidator.Validate(paths, destinationPath, isInternal, sourceDir);

        e.DragEffects = validation.IsValid
            ? (validation.Effect == DropEffect.Copy ? DragDropEffects.Copy : DragDropEffects.Move)
            : DragDropEffects.None;

        var vm = FindViewModel(sender as Control);
        if (vm is not null)
        {
            vm.IsDragOver = validation.IsValid;
            vm.DropTargetPath = validation.IsValid ? destinationPath : null;
        }

        e.Handled = true;
    }

    private static void OnDragLeave(object? sender, DragEventArgs e)
    {
        var vm = FindViewModel(sender as Control);
        if (vm is not null)
        {
            vm.IsDragOver = false;
            vm.DropTargetPath = null;
        }
        e.Handled = true;
    }

    private static async void OnDrop(object? sender, DragEventArgs e)
    {
        var (paths, isInternal, sourceDir) = ExtractDragData(e);
        if (paths.Count == 0) return;

        var destinationPath = GetDropDestination(sender, e);
        var validation = DragDropValidator.Validate(paths, destinationPath, isInternal, sourceDir);

        if (!validation.IsValid) return;

        var vm = FindViewModel(sender as Control);
        if (vm is not null)
        {
            vm.IsDragOver = false;
            vm.DropTargetPath = null;
            await vm.ExecuteDropAsync(paths, destinationPath, validation.Effect);
        }

        e.Handled = true;
    }

    // ================================================================
    // Data extraction
    // ================================================================

    /// <summary>
    /// Extracts source paths from the drag event data.
    /// Supports internal Nexus format and OS file format.
    /// </summary>
    public static (IReadOnlyList<string> Paths, bool IsInternal, string? SourceDirectory) ExtractDragData(DragEventArgs e)
    {
        // Try internal Nexus format
        if (e.Data.Contains(NexusFormatId))
        {
            var raw = e.Data.Get(NexusFormatId) as string;
            if (!string.IsNullOrEmpty(raw))
            {
                var parts = raw.Split("|SOURCE|", 2);
                var paths = parts[0].Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                var sourceDir = parts.Length > 1 ? parts[1] : null;
                return (paths, true, sourceDir);
            }
        }

        // Try OS file format (external drag from Windows Explorer)
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files is not null)
            {
                var paths = files
                    .Select(f => f.Path?.LocalPath)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Cast<string>()
                    .ToList();
                if (paths.Count > 0)
                    return (paths, false, null);
            }
        }

        return (Array.Empty<string>(), false, null);
    }

    // ================================================================
    // Destination resolution
    // ================================================================

    private static string GetDropDestination(object? sender, DragEventArgs e)
    {
        if (e.Source is Control sourceControl)
        {
            var current = sourceControl;
            while (current is not null)
            {
                if (current.DataContext is FileSystemItem item
                    && item.Type == FileSystemItemType.Directory && !item.IsGroupHeader)
                    return item.Path;
                if (current.DataContext is NavigationItem navItem && !navItem.IsSectionHeader
                    && !VirtualPaths.IsVirtual(navItem.Path) && !string.IsNullOrEmpty(navItem.Path))
                    return navItem.Path;
                current = current.Parent as Control;
            }
        }

        var vm = FindViewModel(sender as Control);
        return vm?.CurrentPath ?? string.Empty;
    }

    private static MainWindowViewModel? FindViewModel(Control? control)
    {
        while (control is not null)
        {
            if (control.DataContext is MainWindowViewModel vm)
                return vm;
            control = control.Parent as Control;
        }
        return null;
    }
}

#pragma warning restore CS0618
