using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusExplorer.App.Services;
using NexusExplorer.App.ViewModels;
using NexusExplorer.App.Views;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.DependencyInjection;
using NexusExplorer.Infrastructure.Logging;

namespace NexusExplorer.App;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    /// <summary>The single-instance manager created in Program.Main (null in design time).</summary>
    public static SingleInstanceManager? SingleInstance { get; set; }

    /// <summary>Folder to open on startup, passed via command line (e.g. "Reveal in Explorer").</summary>
    public static string? InitialLaunchPath { get; set; }

    private static readonly ILogger CrashLogger = NexusLog.Create("UnhandledException");

    private static readonly string CrashLogPath = Path.Combine(
        FileLoggerOptions.GetDefaultLogDirectory(), "crash.log");

    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;

    public override void Initialize()
    {
        StartupTiming.Mark("App.Initialize begin");
        AvaloniaXamlLoader.Load(this);
        StartupTiming.Mark("App XAML loaded");

        // Apply the default Light theme immediately so DynamicResource bindings resolve
        ThemeService.Instance.ApplyTheme(ThemeMode.Light);
        StartupTiming.Mark("Theme applied");

        // Wire up global unhandled exception handlers
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        StartupTiming.Mark("OnFrameworkInitializationCompleted begin");
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddNexusExplorer();
        serviceCollection.AddSingleton<NexusExplorer.Core.Abstractions.IIconProvider, NexusExplorer.App.Services.FluentIconProvider>();
        serviceCollection.AddSingleton<NexusExplorer.Core.Abstractions.IFileIconService, NexusExplorer.App.Services.FluentFileIconService>();
        serviceCollection.AddSingleton<NexusExplorer.Core.Abstractions.IThumbnailService, NexusExplorer.App.Services.Thumbnails.ThumbnailService>();
        serviceCollection.AddSingleton<MainWindowViewModel>();

        Services = serviceCollection.BuildServiceProvider();
        StartupTiming.Mark("DI container built");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _viewModel = Services.GetRequiredService<MainWindowViewModel>();
            StartupTiming.Mark("MainWindowViewModel resolved");
            _viewModel.ApplicationExitRequested += ExitApplication;
            _viewModel.OpenSettingsRequested += OpenSettingsWindow;
            _viewModel.ShowPropertiesRequested += OpenPropertiesWindow;
            _mainWindow = new MainWindow { DataContext = _viewModel };
            StartupTiming.Mark("MainWindow created");

            // Route single-instance activation requests and the global hotkey to ShowMainWindow.
            if (SingleInstance is not null)
            {
                SingleInstance.ActivationRequested += path => Dispatcher.UIThread.Post(() => ActivateWithPath(path));
                SingleInstance.StartListening();
            }

            var globalHotkey = Services.GetRequiredService<IGlobalHotkeyService>();
            globalHotkey.HotkeyTriggered += _ => Dispatcher.UIThread.Post(ShowMainWindow);

            desktop.MainWindow = _mainWindow;
            StartupTiming.Mark("MainWindow assigned (will show)");

            // Don't shutdown when the main window closes — minimize to tray instead
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _mainWindow.Closing += OnMainWindowClosing;

            // Wire up tray icon events
            SetupTrayIcon();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon()
    {
        // Get the TrayIcon defined in XAML
        var icons = TrayIcon.GetIcons(this);
        _trayIcon = icons?.FirstOrDefault();

        if (_trayIcon is not null)
        {
            _trayIcon.Clicked += (_, _) => ShowMainWindow();

            // Wire up menu items
            if (_trayIcon.Menu is NativeMenu menu)
            {
                foreach (var item in menu.Items.OfType<NativeMenuItem>())
                {
                    if (item.Header == "Show Nexus Explorer")
                        item.Click += (_, _) => ShowMainWindow();
                    else if (item.Header == "Exit")
                        item.Click += (_, _) => RequestExit();
                }
            }
        }
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // If a file operation is running, don't close or hide silently — ask the user.
        if (_viewModel?.HasActiveOperations == true)
        {
            e.Cancel = true;
            _viewModel.RequestCloseConfirmation();
            return;
        }

        // Always minimize to tray instead of closing — only Exit from tray menu quits.
        // Persist the window state now (while bounds are still valid) so it's remembered even
        // if the process is later killed while sitting in the tray.
        _ = _mainWindow?.SaveWindowStateAsync();

        e.Cancel = true;
        _mainWindow?.Hide();
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = true;
            if (_viewModel?.HasActiveOperations == true)
                _trayIcon.ToolTipText = "Nexus Explorer — File operation in progress...";
            else if (_viewModel?.HasClipboardContent == true)
                _trayIcon.ToolTipText = $"Nexus Explorer — {_viewModel.ClipboardCount} item(s) in clipboard";
            else
                _trayIcon.ToolTipText = "Nexus Explorer — Running in background";
        }
    }

    private void RequestExit()
    {
        if (_viewModel?.HasActiveOperations == true)
        {
            _viewModel.RequestCloseConfirmation();
            return;
        }

        ExitApplication();
    }

    /// <summary>Shows the window and, if a path was supplied, navigates the active tab there.</summary>
    private void ActivateWithPath(string? path)
    {
        ShowMainWindow();

        var target = NexusExplorer.App.Helpers.LaunchArgsHelper.ResolveTargetDirectory(path);
        if (!string.IsNullOrEmpty(target) && _viewModel is not null
            && _viewModel.OpenFolderCommand.CanExecute(target))
        {
            _viewModel.OpenFolderCommand.Execute(target);
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        // Only un-minimize; preserve a maximized window instead of forcing it back to Normal.
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        if (_trayIcon is not null)
            _trayIcon.IsVisible = false;
    }

    private void OpenSettingsWindow()
    {
        if (_mainWindow is null) return;
        var settings = new SettingsWindow { DataContext = _viewModel };
        _ = settings.ShowDialog(_mainWindow);
    }

    private void OpenPropertiesWindow(IReadOnlyList<Core.Models.FileSystemItem> items)
    {
        if (_mainWindow is null || Services is null || items.Count == 0) return;

        var propertiesService = Services.GetRequiredService<IFilePropertiesService>();
        var vm = new PropertiesViewModel(propertiesService, items);

        // "Open location" navigates the main window to the item's folder.
        vm.NavigationRequested += path => Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel?.NavigateToCommand.CanExecute(path) == true)
                _viewModel.NavigateToCommand.Execute(path);
            ShowMainWindow();
        });

        // Refresh the current directory after attribute changes (read-only/hidden).
        vm.AttributesApplied += () => Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel?.RefreshCommand.CanExecute(null) == true)
                _viewModel.RefreshCommand.Execute(null);
        });

        var window = new PropertiesWindow(vm);
        window.Show(_mainWindow);

        // Load properties after the window is shown (size calculation runs async).
        _ = vm.LoadAsync();
    }

    private void ExitApplication()
    {
        // Hide tray icon
        if (_trayIcon is not null)
            _trayIcon.IsVisible = false;

        // Let the window close normally (triggers session save)
        if (_mainWindow is not null)
        {
            _mainWindow.Closing -= OnMainWindowClosing;
            _mainWindow.Close();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogCrash(e.ExceptionObject as Exception, "AppDomain.UnhandledException", isFatal: true);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrash(e.Exception, "TaskScheduler.UnobservedTaskException", isFatal: false);
        e.SetObserved();
    }

    private static void LogCrash(Exception? ex, string source, bool isFatal)
    {
        var message = ex?.Message ?? "Unknown error";

        if (isFatal)
            CrashLogger.LogCritical(ex, "{Source}: {Message}", source, message);
        else
            CrashLogger.LogError(ex, "{Source}: {Message}", source, message);

        // Last-resort synchronous write: on a fatal crash the async log queue may
        // not flush before the process terminates.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var entry = $"[{timestamp}] [{source}]\n{ex?.ToString() ?? "Unknown error"}\n{"".PadRight(80, '-')}\n";
            File.AppendAllText(CrashLogPath, entry);
        }
        catch
        {
            // Cannot log — silently fail
        }
    }
}
