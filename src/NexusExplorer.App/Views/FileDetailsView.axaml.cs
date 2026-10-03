using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NexusExplorer.App.Behaviors;
using NexusExplorer.App.ViewModels;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Views;

public partial class FileDetailsView : UserControl
{
    // Column width properties that item rows bind to
    public static readonly StyledProperty<double> IconColumnWidthProperty =
        AvaloniaProperty.Register<FileDetailsView, double>(nameof(IconColumnWidth), 32);

    public static readonly StyledProperty<double> NameColumnWidthProperty =
        AvaloniaProperty.Register<FileDetailsView, double>(nameof(NameColumnWidth), 400);

    public static readonly StyledProperty<double> ModifiedColumnWidthProperty =
        AvaloniaProperty.Register<FileDetailsView, double>(nameof(ModifiedColumnWidth), 180);

    public static readonly StyledProperty<double> SizeColumnWidthProperty =
        AvaloniaProperty.Register<FileDetailsView, double>(nameof(SizeColumnWidth), 120);

    public static readonly StyledProperty<double> ColorColumnWidthProperty =
        AvaloniaProperty.Register<FileDetailsView, double>(nameof(ColorColumnWidth), 70);

    public double IconColumnWidth
    {
        get => GetValue(IconColumnWidthProperty);
        set => SetValue(IconColumnWidthProperty, value);
    }

    public double NameColumnWidth
    {
        get => GetValue(NameColumnWidthProperty);
        set => SetValue(NameColumnWidthProperty, value);
    }

    public double ModifiedColumnWidth
    {
        get => GetValue(ModifiedColumnWidthProperty);
        set => SetValue(ModifiedColumnWidthProperty, value);
    }

    public double SizeColumnWidth
    {
        get => GetValue(SizeColumnWidthProperty);
        set => SetValue(SizeColumnWidthProperty, value);
    }

    public double ColorColumnWidth
    {
        get => GetValue(ColorColumnWidthProperty);
        set => SetValue(ColorColumnWidthProperty, value);
    }

    // Minimum widths for each data column
    private const double MinNameWidth = 150;
    private const double MinModifiedWidth = 140;
    private const double MinSizeWidth = 100;

    private Grid? _headerGrid;
    private FileSystemItem? _potentialDragItem;

    public FileDetailsView()
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

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { Tag: FileSortMode mode } && DataContext is MainWindowViewModel vm)
        {
            vm.SortByColumnCommand.Execute(mode);
            e.Handled = true;
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _headerGrid = this.FindControl<Grid>("HeaderGrid");
        if (_headerGrid != null)
        {
            // Set min widths on header grid columns
            // Columns: 0=Icon(32), 1=Name(400), 2=Splitter(4), 3=Modified(180), 4=Splitter(4), 5=Size(120)
            _headerGrid.ColumnDefinitions[1].MinWidth = MinNameWidth;
            _headerGrid.ColumnDefinitions[3].MinWidth = MinModifiedWidth;
            _headerGrid.ColumnDefinitions[5].MinWidth = MinSizeWidth;

            // Listen for layout changes to sync column widths to item rows
            _headerGrid.LayoutUpdated += HeaderGrid_LayoutUpdated;
            UpdateColumnWidths();
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_headerGrid != null)
        {
            _headerGrid.LayoutUpdated -= HeaderGrid_LayoutUpdated;
        }
    }

    private void HeaderGrid_LayoutUpdated(object? sender, EventArgs e)
    {
        UpdateColumnWidths();
    }

    private void UpdateColumnWidths()
    {
        if (_headerGrid == null) return;

        var iconW = _headerGrid.ColumnDefinitions[0].ActualWidth;
        var nameW = _headerGrid.ColumnDefinitions[1].ActualWidth;
        var modifiedW = _headerGrid.ColumnDefinitions[3].ActualWidth;
        var sizeW = _headerGrid.ColumnDefinitions[5].ActualWidth;
        var colorW = _headerGrid.ColumnDefinitions[6].ActualWidth;

        // Only update if we have valid widths
        if (nameW <= 0 && modifiedW <= 0 && sizeW <= 0) return;

        if (Math.Abs(IconColumnWidth - iconW) > 0.5)
            IconColumnWidth = iconW;
        if (Math.Abs(NameColumnWidth - nameW) > 0.5)
            NameColumnWidth = nameW;
        if (Math.Abs(ModifiedColumnWidth - modifiedW) > 0.5)
            ModifiedColumnWidth = modifiedW;
        if (Math.Abs(SizeColumnWidth - sizeW) > 0.5)
            SizeColumnWidth = sizeW;
        if (Math.Abs(ColorColumnWidth - colorW) > 0.5)
            ColorColumnWidth = colorW;
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
                    if (item is Core.Models.FileSystemItem fsItem)
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
            if (sender is Control { DataContext: FileSystemItem dragItem } && !dragItem.IsGroupHeader)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    _potentialDragItem = dragItem;
                    DragDropBehavior.BeginDragTracking(e, this);

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
        if (DataContext is not MainWindowViewModel vm || vm.IsRenaming) return;

        if (DragDropBehavior.ShouldStartDrag(e, this))
        {
            var paths = vm.GetDragPaths(_potentialDragItem);
            if (paths.Count > 0)
                DragDropBehavior.StartDrag(this, paths, vm.CurrentPath);
            _potentialDragItem = null;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _potentialDragItem = null;
        DragDropBehavior.EndDragTracking();
    }
}
