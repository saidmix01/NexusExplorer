using Avalonia;

namespace NexusExplorer.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTiming.Mark("Program.Main");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
