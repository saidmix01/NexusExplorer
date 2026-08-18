#pragma warning disable CS0618 // Suppress obsolete warnings for Avalonia DnD API transition

using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NexusExplorer.App.Behaviors;
using NexusExplorer.App.Controls;
using NexusExplorer.App.ViewModels;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Views;

public partial class MainWindow : Window
{
    private DragPreviewControl? _dragPreview;
    private ScrollViewer? _breadcrumbScroll;

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnWindowKeyDown;
        AddHandler(KeyDownEvent, OnWindowPreviewKeyDown, RoutingStrategies.Tunnel);
        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;

        // Register window-level drag events for the drag preview overlay
        // Use Bubble strategy with handledEventsToo so we see events after child handlers process them
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DragLeaveEvent, OnWindowDragLeave, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, OnWindowDrop, RoutingStrategies.Bubble, handledEventsToo: true);

        // Hide preview when drag operation ends (Escape, drop outside, etc.)
        DragDropBehavior.DragEnded += () =>
        {
            if (_dragPreview is not null)
            {
                _dragPreview.Hide();
                _dragPreviewShown = false;
            }
        };
    }

    // ================================================================
    // Window loaded — wire up layout, clipboard, and title bar drag
    // ================================================================

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        StartupTiming.MarkReady("UI ready");

        // Make the custom title bar draggable
        var titleBar = this.FindControl<Border>("TitleBarArea");
        if (titleBar is not null)
        {
            titleBar.PointerPressed += (s, args) =>
            {
                if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(args);
            };
        }

        // Register tab drag-and-drop events
        var tabItemsControl = this.FindControl<ItemsControl>("TabItemsControl");
        if (tabItemsControl is not null)
        {
            tabItemsControl.AddHandler(DragDrop.DragOverEvent, TabItem_DragOver);
            tabItemsControl.AddHandler(DragDrop.DropEvent, TabItem_Drop);
        }

        // Restore window bounds and wire up ViewModel events
        if (DataContext is MainWindowViewModel vm)
        {
            _ = RestoreWindowBoundsAsync(vm);

            vm.CopyTextToClipboardRequested += async text =>
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is not null)
                    await clipboard.SetTextAsync(text);
            };

            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(MainWindowViewModel.LayoutMode)
                    or nameof(MainWindowViewModel.SplitOrientation))
                    UpdateContentLayout(vm);
                if (args.PropertyName == nameof(MainWindowViewModel.IsPreviewVisible))
                    UpdatePreviewLayout(vm.IsPreviewVisible);
                if (args.PropertyName == nameof(MainWindowViewModel.IsCreateDialogVisible) && vm.IsCreateDialogVisible)
                    FocusCreateDialogTextBox();
                if (args.PropertyName == nameof(MainWindowViewModel.IsGroupDialogVisible) && vm.IsGroupDialogVisible)
                    FocusGroupDialogTextBox();
            };
            UpdateContentLayout(vm);
            UpdatePreviewLayout(vm.IsPreviewVisible);

            // Auto-scroll the breadcrumb to the right end so the current folder
            // (last segment) stays visible when the path grows.
            _breadcrumbScroll = this.FindControl<ScrollViewer>("BreadcrumbScrollViewer");
            if (_breadcrumbScroll is not null)
            {
                // Defer the scroll until after the layout pass completes. Scrolling
                // synchronously (or on the first LayoutUpdated) reads a stale
                // ScrollBarMaximum because the new segments aren't measured yet,
                // which leaves the current folder hidden off-screen.
                vm.Breadcrumbs.CollectionChanged += (_, _) =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        ScrollBreadcrumbToEnd,
                        Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        _dragPreview = this.FindControl<DragPreviewControl>("DragPreview");
    }

    private void ScrollBreadcrumbToEnd()
    {
        if (_breadcrumbScroll is not null)
            _breadcrumbScroll.Offset = _breadcrumbScroll.ScrollBarMaximum;
    }

    // ================================================================
    // Drag Preview — window-level handlers for visual feedback
    // ================================================================

    private bool _dragPreviewShown;

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        if (_dragPreview is null) return;

        var (paths, isInternal, sourceDir) = DragDropBehavior.ExtractDragData(e);
        if (paths.Count == 0) return;

        // Show drag preview on first DragOver
        if (!_dragPreviewShown)
        {
            _dragPreviewShown = true;
            var primaryItem = GetPrimaryDragItem(paths);
            _dragPreview.Show(paths, primaryItem);
        }

        // Update position to follow cursor
        var pos = e.GetPosition(this);
        _dragPreview.UpdatePosition(pos);

        // Update valid/invalid visual state
        var isValid = e.DragEffects != DragDropEffects.None;
        _dragPreview.SetValidTarget(isValid);
    }

    private void OnWindowDragLeave(object? sender, DragEventArgs e)
    {
        // DragLeave fires when moving between children within the window.
        // Only hide if the pointer actually left the window bounds.
        // We use a brief delay — if DragOver fires again quickly, we cancel the hide.
        if (_dragPreview is not null && _dragPreviewShown)
        {
            // Check if the pointer is still within the window bounds
            var pos = e.GetPosition(this);
            if (pos.X < 0 || pos.Y < 0 || pos.X > Bounds.Width || pos.Y > Bounds.Height)
            {
                _dragPreview.Hide();
                _dragPreviewShown = false;
            }
        }
    }

    private void OnWindowDrop(object? sender, DragEventArgs e)
    {
        if (_dragPreview is not null)
        {
            _dragPreview.Hide();
            _dragPreviewShown = false;
        }
    }

    /// <summary>
    /// Tries to find the FileSystemItem for the first dragged path from the current Items collection.
    /// Used to determine the icon/thumbnail for the drag preview.
    /// </summary>
    private FileSystemItem? GetPrimaryDragItem(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return null;
        if (DataContext is not MainWindowViewModel vm) return null;

        var firstPath = paths[0];
        // Look in current items
        var item = vm.Items.FirstOrDefault(i => string.Equals(i.Path, firstPath, StringComparison.OrdinalIgnoreCase));
        if (item is not null) return item;

        // Create a minimal FileSystemItem from the path for icon resolution
        var name = System.IO.Path.GetFileName(firstPath);
        var ext = System.IO.Path.GetExtension(firstPath);
        var isDir = System.IO.Directory.Exists(firstPath);

        return new FileSystemItem
        {
            Name = name ?? firstPath,
            Path = firstPath,
            Type = isDir ? FileSystemItemType.Directory : FileSystemItemType.File,
            Extension = isDir ? null : ext
        };
    }

    // ================================================================
    // Traffic light buttons: Close / Minimize / Maximize
    // ================================================================

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    // ================================================================
    // Session persistence — save on close, restore on load
    // ================================================================

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var bounds = Bounds;
            var pos = Position;
            await vm.SaveSessionAsync(
                bounds.Width,
                bounds.Height,
                pos.X,
                pos.Y,
                WindowState == WindowState.Maximized);
        }
    }

    private async Task RestoreWindowBoundsAsync(MainWindowViewModel vm)
    {
        try
        {
            var windowState = await vm.GetPersistedWindowStateAsync();
            if (windowState is null) return;

            if (windowState.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                Width = windowState.Width;
                Height = windowState.Height;

                if (!double.IsNaN(windowState.X) && !double.IsNaN(windowState.Y))
                {
                    Position = new PixelPoint((int)windowState.X, (int)windowState.Y);
                }
            }
        }
        catch
        {
            // Ignore restore failures — use defaults
        }
    }

    // ================================================================
    // Content layout management (Explorer / Terminal / Split)
    // ================================================================

    private void UpdateContentLayout(MainWindowViewModel vm)
    {
        var contentGrid = this.FindControl<Grid>("ContentGrid");
        if (contentGrid is null) return;
        if (contentGrid.ColumnDefinitions.Count < 3 || contentGrid.RowDefinitions.Count < 3) return;

        var explorerGrid = this.FindControl<Grid>("ExplorerGrid");
        var terminalPanel = this.FindControl<UserControl>("TerminalPanel");
        var splitter = this.FindControl<GridSplitter>("SplitDivider");

        var mode = vm.LayoutMode;
        var orientation = vm.SplitOrientation;

        // Reset all grid positions to column-based defaults
        if (explorerGrid is not null) { Grid.SetColumn(explorerGrid, 0); Grid.SetRow(explorerGrid, 0); Grid.SetColumnSpan(explorerGrid, 1); Grid.SetRowSpan(explorerGrid, 1); }
        if (splitter is not null) { Grid.SetColumn(splitter, 1); Grid.SetRow(splitter, 0); Grid.SetColumnSpan(splitter, 1); Grid.SetRowSpan(splitter, 1); }
        if (terminalPanel is not null) { Grid.SetColumn(terminalPanel, 2); Grid.SetRow(terminalPanel, 0); Grid.SetColumnSpan(terminalPanel, 1); Grid.SetRowSpan(terminalPanel, 1); }

        switch (mode)
        {
            case LayoutMode.ExplorerOnly:
                // All columns to explorer, rows collapsed
                contentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                contentGrid.ColumnDefinitions[1].Width = new GridLength(0);
                contentGrid.ColumnDefinitions[2].Width = new GridLength(0);
                contentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                contentGrid.RowDefinitions[1].Height = new GridLength(0);
                contentGrid.RowDefinitions[2].Height = new GridLength(0);

                if (explorerGrid is not null) explorerGrid.IsVisible = true;
                if (terminalPanel is not null) terminalPanel.IsVisible = false;
                if (splitter is not null) splitter.IsVisible = false;
                break;

            case LayoutMode.TerminalOnly:
                contentGrid.ColumnDefinitions[0].Width = new GridLength(0);
                contentGrid.ColumnDefinitions[1].Width = new GridLength(0);
                contentGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                contentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                contentGrid.RowDefinitions[1].Height = new GridLength(0);
                contentGrid.RowDefinitions[2].Height = new GridLength(0);

                if (explorerGrid is not null) explorerGrid.IsVisible = false;
                if (terminalPanel is not null) { terminalPanel.IsVisible = true; Grid.SetColumn(terminalPanel, 0); Grid.SetColumnSpan(terminalPanel, 3); }
                if (splitter is not null) splitter.IsVisible = false;
                break;

            case LayoutMode.Split when orientation == SplitOrientation.Vertical:
                // Side by side: Explorer left | Splitter | Terminal right (columns)
                contentGrid.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
                contentGrid.ColumnDefinitions[1].Width = new GridLength(5, GridUnitType.Pixel);
                contentGrid.ColumnDefinitions[2].Width = new GridLength(2, GridUnitType.Star);
                contentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                contentGrid.RowDefinitions[1].Height = new GridLength(0);
                contentGrid.RowDefinitions[2].Height = new GridLength(0);

                if (explorerGrid is not null) explorerGrid.IsVisible = true;
                if (terminalPanel is not null) terminalPanel.IsVisible = true;
                if (splitter is not null)
                {
                    splitter.IsVisible = true;
                    splitter.Width = 5;
                    splitter.Height = double.NaN;
                    splitter.ResizeDirection = Avalonia.Controls.GridResizeDirection.Columns;
                }
                break;

            case LayoutMode.Split: // Horizontal orientation
                // Stacked: Explorer top | Splitter | Terminal bottom (rows)
                contentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                contentGrid.ColumnDefinitions[1].Width = new GridLength(0);
                contentGrid.ColumnDefinitions[2].Width = new GridLength(0);
                contentGrid.RowDefinitions[0].Height = new GridLength(3, GridUnitType.Star);
                contentGrid.RowDefinitions[1].Height = new GridLength(5, GridUnitType.Pixel);
                contentGrid.RowDefinitions[2].Height = new GridLength(2, GridUnitType.Star);

                if (explorerGrid is not null) { explorerGrid.IsVisible = true; Grid.SetColumnSpan(explorerGrid, 3); }
                if (splitter is not null)
                {
                    splitter.IsVisible = true;
                    Grid.SetRow(splitter, 1);
                    Grid.SetColumn(splitter, 0);
                    Grid.SetColumnSpan(splitter, 3);
                    splitter.Width = double.NaN;
                    splitter.Height = 5;
                    splitter.ResizeDirection = Avalonia.Controls.GridResizeDirection.Rows;
                }
                if (terminalPanel is not null)
                {
                    terminalPanel.IsVisible = true;
                    Grid.SetRow(terminalPanel, 2);
                    Grid.SetColumn(terminalPanel, 0);
                    Grid.SetColumnSpan(terminalPanel, 3);
                }
                break;
        }
    }

    private void UpdateContentLayout(LayoutMode mode)
    {
        if (DataContext is MainWindowViewModel vm)
            UpdateContentLayout(vm);
    }

    // ================================================================
    // Preview pane layout
    // ================================================================

    private void UpdatePreviewLayout(bool isPreviewVisible)
    {
        var explorerGrid = this.FindControl<Grid>("ExplorerGrid");
        if (explorerGrid is null || explorerGrid.ColumnDefinitions.Count < 3) return;

        if (isPreviewVisible)
        {
            // Use star-based sizing so the GridSplitter works for user resizing
            explorerGrid.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
            explorerGrid.ColumnDefinitions[1].Width = new GridLength(5, GridUnitType.Pixel);
            explorerGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            explorerGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            explorerGrid.ColumnDefinitions[1].Width = new GridLength(0);
            explorerGrid.ColumnDefinitions[2].Width = new GridLength(0);
        }
    }

    // ================================================================
    // Terminal focus detection
    // ================================================================

    private bool IsTerminalFocused()
    {
        var focused = FocusManager?.GetFocusedElement();
        if (focused is Visual visual)
        {
            var terminalPanel = this.FindControl<TerminalView>("TerminalPanel");
            if (terminalPanel is not null)
            {
                var current = visual as Control;
                while (current is not null)
                {
                    if (ReferenceEquals(current, terminalPanel))
                        return true;
                    current = current.Parent as Control;
                }
            }
        }
        return false;
    }

    // ================================================================
    // Tab interactions
    // ================================================================

    private void Tab_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: Core.Models.TabItem tab }
            && DataContext is MainWindowViewModel vm)
        {
            vm.ActivateTabCommand.Execute(tab.Id);
            // Start tracking for potential drag-reorder
            _tabDragStart = e.GetPosition(this);
            _draggedTab = tab;
            e.Handled = true;
        }
    }

    private void TabClose_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Core.Models.TabItem tab }
            && DataContext is MainWindowViewModel vm)
        {
            vm.CloseTabCommand.Execute(tab.Id);
            e.Handled = true;
        }
    }

    // ================================================================
    // Tab context menu handlers
    // ================================================================

    private void TabCtx_DuplicateTab(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: Core.Models.TabItem tab }
            && DataContext is MainWindowViewModel vm)
        {
            vm.DuplicateTabCommand.Execute(tab.Id);
        }
    }

    private void TabCtx_CloseTab(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: Core.Models.TabItem tab }
            && DataContext is MainWindowViewModel vm)
        {
            vm.CloseTabCommand.Execute(tab.Id);
        }
    }

    private void TabCtx_CloseOtherTabs(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: Core.Models.TabItem tab }
            && DataContext is MainWindowViewModel vm)
        {
            vm.CloseOtherTabsCommand.Execute(tab.Id);
        }
    }

    private void TabCtx_CloseAllTabs(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseAllTabsCommand.Execute(null);
        }
    }

    // ================================================================
    // Move To dialog — folder navigation
    // ================================================================

    private void MoveTo_FolderDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox listBox
            && listBox.SelectedItem is Core.Models.FileSystemItem item
            && item.Type == Core.Models.FileSystemItemType.Directory
            && DataContext is MainWindowViewModel vm)
        {
            vm.MoveToNavigateCommand.Execute(item.Path);
        }
    }

    private void TabBar_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.NewTabCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void TabScroll_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            // Convert vertical wheel to horizontal scroll
            var offset = sv.Offset;
            var delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            sv.Offset = offset.WithX(offset.X - delta * 50);
            e.Handled = true;
        }
    }

    // ================================================================
    // Tab drag-and-drop reordering
    // ================================================================

    private Point _tabDragStart;
    private bool _isTabDragging;
    private Core.Models.TabItem? _draggedTab;

    private void Tab_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedTab is null || _isTabDragging) return;

        var pos = e.GetPosition(this);
        var delta = pos - _tabDragStart;
        if (Math.Abs(delta.X) > 8 || Math.Abs(delta.Y) > 8)
        {
            _isTabDragging = true;
            // Start a drag operation with the tab ID
            var dataObject = new DataObject();
            dataObject.Set("NexusTabId", _draggedTab.Id);
            _ = DragDrop.DoDragDrop(e, dataObject, DragDropEffects.Move);
            _isTabDragging = false;
            _draggedTab = null;
        }
    }

    private void Tab_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _draggedTab = null;
        _isTabDragging = false;
    }

    private void TabItem_DragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("NexusTabId"))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void TabItem_Drop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("NexusTabId")) return;
        if (sender is not Border { DataContext: Core.Models.TabItem targetTab }) return;
        if (DataContext is not MainWindowViewModel vm) return;

        var sourceTabId = e.Data.Get("NexusTabId") as string;
        if (string.IsNullOrEmpty(sourceTabId) || sourceTabId == targetTab.Id) return;

        var tabs = vm.Tabs;
        var fromIndex = -1;
        var toIndex = -1;
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].Id == sourceTabId) fromIndex = i;
            if (tabs[i].Id == targetTab.Id) toIndex = i;
        }

        if (fromIndex >= 0 && toIndex >= 0)
            vm.MoveTabCommand.Execute(new[] { fromIndex, toIndex });

        e.Handled = true;
    }

    // ================================================================
    // Sidebar navigation
    // ================================================================

    private void SidebarListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox
            && listBox.SelectedItem is NavigationItem item
            && DataContext is MainWindowViewModel vm)
        {
            vm.SidebarNavigateCommand.Execute(item);
            listBox.SelectedItem = null; // Reset so same item can be re-selected
        }
    }

    private void SidebarItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: NavigationItem item }
            && DataContext is MainWindowViewModel vm)
        {
            vm.SidebarNavigateCommand.Execute(item);
            e.Handled = true;
        }
    }

    private void SidebarGroupHeader_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SidebarGroup group }
            && DataContext is MainWindowViewModel vm)
        {
            vm.ToggleGroupCommand.Execute(group);
        }
    }

    private void GroupDialogTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            vm.ConfirmGroupDialogCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelGroupDialogCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ================================================================
    // Address bar editing
    // ================================================================

    private void AddressBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // No longer used — double-click triggers edit instead
    }

    private void AddressBar_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StartAddressBarEditCommand.Execute(null);
            var textBox = this.FindControl<TextBox>("AddressBarTextBox");
            if (textBox is not null)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }
    }

    private void AddressBar_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            var textBox = sender as TextBox;
            vm.NavigateToAddressCommand.Execute(textBox?.Text);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelAddressBarEditCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            // Autocomplete: complete the path with Tab key
            var textBox = sender as TextBox;
            if (textBox is not null)
            {
                var suggestion = vm.GetAutocompleteSuggestion(textBox.Text ?? "");
                if (suggestion is not null)
                {
                    vm.AddressBarText = suggestion + Path.DirectorySeparatorChar;
                    textBox.CaretIndex = textBox.Text?.Length ?? 0;
                }
            }
            e.Handled = true;
        }
    }

    private void AddressBar_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.IsAddressBarEditing)
        {
            vm.CancelAddressBarEditCommand.Execute(null);
        }
    }

    // ================================================================
    // Search box
    // ================================================================

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.ClearSearchCommand.Execute(null);
            }
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && DataContext is MainWindowViewModel vm)
        {
            // Ensure the ViewModel's SearchQuery stays in sync even if binding is delayed
            var text = tb.Text ?? string.Empty;
            if (vm.SearchQuery != text)
            {
                vm.SearchQuery = text;
            }
        }
    }

    // ================================================================
    // Create dialog
    // ================================================================

    private void CreateDialogTextBox_Attached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // No-op: focus is handled by watching IsCreateDialogVisible in OnWindowLoaded
    }

    private void CreateDialogTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            vm.ConfirmCreateCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelCreateCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FocusCreateDialogTextBox()
    {
        var tb = this.FindControl<TextBox>("CreateDialogTextBox");
        if (tb is not null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private void FocusGroupDialogTextBox()
    {
        var tb = this.FindControl<TextBox>("GroupDialogTextBox");
        if (tb is not null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    // ================================================================
    // Keyboard shortcuts — Tunnel phase (before children)
    // ================================================================

    private void OnWindowPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Don't intercept when terminal or text input has focus
        var isTerminalFocused = IsTerminalFocused();
        var focusedElement = FocusManager?.GetFocusedElement();
        var isTextInput = focusedElement is TextBox;
        if (isTerminalFocused || isTextInput) return;

        // File operation shortcuts (Tunnel so they fire before ListBox consumes them)
        switch (e.KeyModifiers)
        {
            case KeyModifiers.Control when e.Key == Key.C:
                vm.CopySelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control when e.Key == Key.X:
                vm.CutSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control when e.Key == Key.V:
                vm.PasteCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control when e.Key == Key.A:
                vm.SelectedItems.Clear();
                foreach (var item in vm.Items)
                    vm.SelectedItems.Add(item);
                e.Handled = true;
                break;
            case KeyModifiers.Control | KeyModifiers.Shift when e.Key == Key.C:
                vm.CopyPathCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control | KeyModifiers.Shift when e.Key == Key.N:
                vm.CreateFolderCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Shift when e.Key == Key.Delete:
                vm.PermanentDeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.None when e.Key == Key.Delete:
                vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.None when e.Key == Key.F2:
                vm.StartRenameCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // ================================================================
    // Keyboard shortcuts — Bubble phase (after children)
    // ================================================================

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var focusedElement = FocusManager?.GetFocusedElement();
        var isTextInput = focusedElement is TextBox;
        var isTerminalFocused = IsTerminalFocused();

        // Ctrl+` — toggle terminal (always works)
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.OemTilde)
        {
            vm.ToggleTerminalCommand.Execute(null);
            if (vm.IsTerminalVisible)
            {
                var terminalPanel = this.FindControl<TerminalView>("TerminalPanel");
                terminalPanel?.FocusTerminal();
            }
            e.Handled = true;
            return;
        }

        // Ctrl+L — focus address bar (works even from terminal)
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.L)
        {
            vm.StartAddressBarEditCommand.Execute(null);
            var textBox = this.FindControl<TextBox>("AddressBarTextBox");
            if (textBox is not null)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
            e.Handled = true;
            return;
        }

        // Ctrl+F — focus search box
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.F)
        {
            var searchBox = this.FindControl<TextBox>("SearchBoxTextBox");
            if (searchBox is not null)
            {
                searchBox.Focus();
                searchBox.SelectAll();
            }
            e.Handled = true;
            return;
        }

        // When terminal has focus, let it handle all other input
        if (isTerminalFocused) return;

        // Don't intercept other shortcuts when in text input
        if (isTextInput) return;

        switch (e.KeyModifiers)
        {
            case KeyModifiers.Control when e.Key == Key.T:
                vm.NewTabCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control when e.Key == Key.W:
                vm.CloseActiveTabCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control when e.Key == Key.Tab:
                vm.NextTabCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control | KeyModifiers.Shift when e.Key == Key.Tab:
                vm.PreviousTabCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Alt when e.Key == Key.Left:
                vm.GoBackCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Alt when e.Key == Key.Right:
                vm.GoForwardCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Alt when e.Key == Key.Up:
                vm.GoUpCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.None when e.Key == Key.F5:
                vm.RefreshCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control when e.Key == Key.Z:
                vm.UndoCommand.Execute(null);
                e.Handled = true;
                break;
            // Redo: Ctrl+Shift+Z or Ctrl+Y
            case KeyModifiers.Control | KeyModifiers.Shift when e.Key == Key.Z:
                vm.RedoCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control when e.Key == Key.Y:
                vm.RedoCommand.Execute(null);
                e.Handled = true;
                break;
            // Ctrl+D — Duplicate selected items
            case KeyModifiers.Control when e.Key == Key.D:
                vm.DuplicateSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            // Ctrl+H — Toggle hidden files
            case KeyModifiers.Control when e.Key == Key.H:
                vm.ToggleShowHiddenFilesCommand.Execute(null);
                e.Handled = true;
                break;
            // Ctrl+P — Toggle preview panel
            case KeyModifiers.Control when e.Key == Key.P:
                vm.TogglePreviewCommand.Execute(null);
                e.Handled = true;
                break;
            // View mode shortcuts: Ctrl+Shift+1-6
            case KeyModifiers.Control | KeyModifiers.Shift when e.Key == Key.D1:
                vm.SetExtraLargeIconsViewCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control | KeyModifiers.Shift when e.Key == Key.D2:
                vm.SetLargeIconsViewCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control | KeyModifiers.Shift when e.Key == Key.D3:
                vm.SetMediumIconsViewCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control | KeyModifiers.Shift when e.Key == Key.D4:
                vm.SetSmallIconsViewCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control | KeyModifiers.Shift when e.Key == Key.D5:
                vm.SetListViewCommand.Execute(null);
                e.Handled = true;
                break;
            case KeyModifiers.Control | KeyModifiers.Shift when e.Key == Key.D6:
                vm.SetDetailsViewCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
