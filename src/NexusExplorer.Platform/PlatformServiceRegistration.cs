using Microsoft.Extensions.DependencyInjection;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Platform.Linux;
using NexusExplorer.Platform.MacOS;
using NexusExplorer.Platform.Windows;

namespace NexusExplorer.Platform;

/// <summary>
/// Registers platform-specific services based on the current OS.
/// </summary>
public static class PlatformServiceRegistration
{
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        switch (PlatformDetector.Current)
        {
            case PlatformKind.Windows:
                services.AddSingleton<IPlatformService, WindowsPlatformService>();
                services.AddSingleton<ITerminalService, WindowsTerminalService>();
                services.AddSingleton<ITerminalSessionService, WindowsTerminalSessionService>();
                services.AddSingleton<IRecycleBinService, WindowsRecycleBinService>();
                services.AddSingleton<ISendToService, WindowsSendToService>();
                break;
            case PlatformKind.Linux:
                services.AddSingleton<IPlatformService, LinuxPlatformService>();
                services.AddSingleton<ITerminalService, LinuxTerminalService>();
                services.AddSingleton<ITerminalSessionService, LinuxTerminalSessionService>();
                services.AddSingleton<IRecycleBinService, LinuxRecycleBinService>();
                services.AddSingleton<ISendToService, DefaultSendToService>();
                break;
            case PlatformKind.MacOS:
                services.AddSingleton<IPlatformService, MacOSPlatformService>();
                services.AddSingleton<ITerminalService, MacOSTerminalService>();
                services.AddSingleton<ITerminalSessionService, MacOSTerminalSessionService>();
                services.AddSingleton<IRecycleBinService, MacOSRecycleBinService>();
                services.AddSingleton<ISendToService, DefaultSendToService>();
                break;
            default:
                throw new PlatformNotSupportedException(
                    "The current operating system is not supported.");
        }

        return services;
    }
}
