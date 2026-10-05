using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace NexusExplorer.App.Views;

/// <summary>
/// A small themed popup window used as the system-tray context menu, so the tray menu matches
/// the rest of the app's UI (icons, rounded, glass) instead of the plain OS NativeMenu.
/// Shown near the cursor; auto-closes when it loses focus.
/// </summary>
public partial class TrayMenuWindow : Window
{
    public event Action? ShowRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayMenuWindow()
    {
        InitializeComponent();

        this.FindControl<Button>("ShowItem")!.Click += (_, _) => Fire(ShowRequested);
        this.FindControl<Button>("SettingsItem")!.Click += (_, _) => Fire(SettingsRequested);
        this.FindControl<Button>("ExitItem")!.Click += (_, _) => Fire(ExitRequested);

        // Dismiss when the user clicks elsewhere / the popup loses focus.
        Deactivated += (_, _) => Hide();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Fire(Action? action)
    {
        Hide();
        action?.Invoke();
    }
}
