using Avalonia;
using NexusExplorer.App.Helpers;
using NexusExplorer.App.Services;

namespace NexusExplorer.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTiming.Mark("Program.Main");

        // A folder to open, passed by the shell / another app (e.g. VS Code "Reveal in Explorer").
        var launchPath = LaunchArgsHelper.ResolveTargetDirectory(args);

        // Ensure only one instance runs; if another is already active, hand it the requested
        // path (so it navigates there) and exit this new process immediately.
        using var singleInstance = new SingleInstanceManager();
        if (!singleInstance.IsFirstInstance)
        {
            SingleInstanceManager.RequestActivation(launchPath);
            return;
        }

        App.SingleInstance = singleInstance;
        App.InitialLaunchPath = launchPath;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
