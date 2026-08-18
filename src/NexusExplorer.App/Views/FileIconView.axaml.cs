using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NexusExplorer.App.Behaviors;
using NexusExplorer.App.ViewModels;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Views;

public partial class FileIconView : UserControl
{
    private FileSystemItem? _potentialDragItem;

    public FileIconView()
    {
        InitializeComponent();
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
    }

    private void AddToGroupItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SidebarGroup group } && DataContext is MainWindowViewModel vm)
            vm.AddToGroupCommand.Execute(group);
    }

    private void RenameTextBox_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            if (textBox.IsVisible)
                FocusRenameTextBox(textBox);

            textBox.PropertyChanged += (s, args) =>
            {
                if (args.Property == Visual.IsVisibleProperty && s is TextBox tb && tb.IsVisible)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => FocusRenameTextBox(tb), Avalonia.Threading.DispatcherPriority.Input);
                }
            };
        }
    }

    private static void FocusRenameTextBox(TextBox textBox)
    {
        textBox.Focus();
        var text = textBox.Text ?? string.Empty;
        var extIndex = text.LastIndexOf('.');
        if (extIndex > 0)
        {
            textBox.SelectionStart = 0;
            textBox.SelectionEnd = extIndex;
        }
        else
        {
            textBox.SelectAll();
        }
    }

    private void RenameTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (e.Key == Key.Enter)
            {
                vm.ConfirmRenameCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                vm.CancelRenameCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void RenameTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.IsRenaming)
        {
            vm.ConfirmRenameCommand.Execute(null);
        }
    }

    private void FileList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedItem is not null)
        {
            vm.OpenItemCommand.Execute(vm.SelectedItem);
        }
    }

    private void FileList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && DataContext is MainWindowViewModel vm)
        {
            vm.SelectedItems.Clear();
            if (listBox.SelectedItems is not null)
            {
                foreach (var item in listBox.SelectedItems)
                {
                    if (item is FileSystemItem fsItem)
                        vm.SelectedItems.Add(fsItem);
                }
            }
        }
    }

    private void Item_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        // Cancel any active rename when clicking on any item
        if (DataContext is MainWindowViewModel vmCheck && vmCheck.IsRenaming)
        {
            vmCheck.CancelRenameCommand.Execute(null);
        }

        if (point.Properties.IsRightButtonPressed)
        {
            // Right click: context menu handling
            if (sender is Control control && control.DataContext is FileSystemItem item
                && DataContext is MainWindowViewModel vm)
            {
                if (!vm.SelectedItems.Contains(item))
                {
                    var listBox = this.FindControl<ListBox>("FileListBox");
                    if (listBox != null)
                    {
                        listBox.SelectedItems?.Clear();
                        listBox.SelectedItems?.Add(item);
                    }
                    vm.SelectedItem = item;
                }
            }
            return;
        }

        if (point.Properties.IsLeftButtonPressed)
        {
            // Track potential drag start
            if (sender is Control { DataContext: FileSystemItem dragItem } && !dragItem.IsGroupHeader)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    _potentialDragItem = dragItem;
                    DragDropBehavior.BeginDragTracking(e, this);

                    // If this item is already part of a multi-selection,
                    // prevent the ListBox from deselecting other items on pointer press.
                    // The selection will be preserved for dragging all selected items.
                    if (vm.SelectedItems.Count > 1 && vm.SelectedItems.Contains(dragItem))
                    {
                        e.Handled = true;
                    }
                }
            }
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_potentialDragItem is null) return;
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.IsRenaming) return;

        if (DragDropBehavior.ShouldStartDrag(e, this))
        {
            var paths = vm.GetDragPaths(_potentialDragItem);
            if (paths.Count > 0)
            {
                DragDropBehavior.StartDrag(this, paths, vm.CurrentPath);
            }
            _potentialDragItem = null;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _potentialDragItem = null;
        DragDropBehavior.EndDragTracking();
    }
}
