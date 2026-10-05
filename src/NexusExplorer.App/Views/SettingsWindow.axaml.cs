using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NexusExplorer.App.Helpers;
using NexusExplorer.App.ViewModels;

namespace NexusExplorer.App.Views;

public partial class SettingsWindow : Window
{
    private bool _ready;

    public SettingsWindow()
    {
        InitializeComponent();
        KeyDown += OnCaptureKeyDown;

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

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ready = true;
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void EnableHotkey_Changed(object? sender, RoutedEventArgs e)
    {
        // Ignore the IsCheckedChanged that fires during initial binding (before the
        // window opens), otherwise opening Settings would spuriously re-register/disable.
        if (!_ready || DataContext is not MainWindowViewModel vm) return;
        _ = vm.ApplyGlobalHotkeyEnabledAsync();
    }

    private void StartWithWindows_Changed(object? sender, RoutedEventArgs e)
    {
        if (!_ready || DataContext is not MainWindowViewModel vm) return;
        _ = vm.ApplyStartWithWindowsAsync();
    }

    private void OnCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.IsGlobalHotkeyCapturing) return;

        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            vm.CancelGlobalHotkeyCaptureCommand.Execute(null);
            return;
        }

        // Ignore pure modifier presses (wait for the actual key).
        if (HotkeyCaptureHelper.IsModifier(e.Key)) return;

        if (e.KeyModifiers == KeyModifiers.None)
        {
            vm.GlobalHotkeyStatus = "Use at least one modifier (Ctrl, Alt, Shift or Win).";
            return;
        }

        var shortcut = HotkeyCaptureHelper.ToShortcut(e.KeyModifiers, e.Key);
        vm.CancelGlobalHotkeyCaptureCommand.Execute(null);
        _ = vm.ApplyGlobalHotkeyShortcutAsync(shortcut);
    }
}
