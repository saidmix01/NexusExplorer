using Avalonia;
using NexusExplorer.App.Services;

namespace NexusExplorer.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTiming.Mark("Program.Main");

        // Ensure only one instance runs; if another is already active, ask it to
        // show itself and exit this new process immediately.
        using var singleInstance = new SingleInstanceManager();
        if (!singleInstance.IsFirstInstance)
        {
            SingleInstanceManager.RequestActivation();
            return;
        }

        App.SingleInstance = singleInstance;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
