using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NexusExplorer.App.ViewModels;

namespace NexusExplorer.App.Views;

public partial class PropertiesWindow : Window
{
    public PropertiesWindow()
    {
        InitializeComponent();

        // Custom title bar drag (SystemDecorations=None uses our own chrome).
        var titleBar = this.FindControl<Border>("TitleBarArea");
        if (titleBar is not null)
        {
            titleBar.PointerPressed += (_, args) =>
            {
                if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(args);
            };
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

    public PropertiesWindow(PropertiesViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += () => Close();

        viewModel.CopyPathRequested += async path =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(path);
        };

        viewModel.OpenLocationRequested += path =>
        {
            // Navigate in the main window by closing properties and triggering navigation
            Close();
            var dir = System.IO.File.Exists(path) ? System.IO.Path.GetDirectoryName(path) : path;
            if (!string.IsNullOrEmpty(dir))
            {
                // The main window will pick up the navigation via the ViewModel
                viewModel.RequestNavigation(dir);
            }
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is PropertiesViewModel vm)
        {
            vm.CancelCalculation();
        }
        base.OnClosed(e);
    }
}
