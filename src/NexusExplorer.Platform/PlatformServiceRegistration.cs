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
                services.AddSingleton<ITerminalDiscoveryService, WindowsTerminalDiscoveryService>();
                services.AddSingleton<IGlobalHotkeyService, WindowsGlobalHotkeyService>();
                services.AddSingleton<IRecycleBinService, WindowsRecycleBinService>();
                if (OperatingSystem.IsWindows())
                    services.AddSingleton<IRecycleBinQueryService, WindowsRecycleBinQueryService>();
                else
                    services.AddSingleton<IRecycleBinQueryService, UnsupportedRecycleBinQueryService>();
                services.AddSingleton<ISendToService, WindowsSendToService>();
                if (OperatingSystem.IsWindows())
                    services.AddSingleton<IShellMetadataProvider, WindowsShellPropertyProvider>();
                break;
            case PlatformKind.Linux:
                services.AddSingleton<IPlatformService, LinuxPlatformService>();
                services.AddSingleton<ITerminalService, LinuxTerminalService>();
                services.AddSingleton<ITerminalSessionService, LinuxTerminalSessionService>();
                services.AddSingleton<ITerminalDiscoveryService, LinuxTerminalDiscoveryService>();
                services.AddSingleton<IGlobalHotkeyService, LinuxGlobalHotkeyService>();
                services.AddSingleton<IRecycleBinService, LinuxRecycleBinService>();
                services.AddSingleton<IRecycleBinQueryService, UnsupportedRecycleBinQueryService>();
                services.AddSingleton<ISendToService, DefaultSendToService>();
                break;
            case PlatformKind.MacOS:
                services.AddSingleton<IPlatformService, MacOSPlatformService>();
                services.AddSingleton<ITerminalService, MacOSTerminalService>();
                services.AddSingleton<ITerminalSessionService, MacOSTerminalSessionService>();
                services.AddSingleton<ITerminalDiscoveryService, MacOSTerminalDiscoveryService>();
                services.AddSingleton<IGlobalHotkeyService, MacOSGlobalHotkeyService>();
                services.AddSingleton<IRecycleBinService, MacOSRecycleBinService>();
                services.AddSingleton<IRecycleBinQueryService, UnsupportedRecycleBinQueryService>();
                services.AddSingleton<ISendToService, DefaultSendToService>();
                break;
            default:
                throw new PlatformNotSupportedException(
                    "The current operating system is not supported.");
        }

        return services;
    }
}
