using System.Runtime.InteropServices;

namespace NexusExplorer.Platform;

/// <summary>
/// Detects the current operating system platform.
/// </summary>
public static class PlatformDetector
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static PlatformKind Current
    {
        get
        {
            if (IsWindows) return PlatformKind.Windows;
            if (IsMacOS) return PlatformKind.MacOS;
            if (IsLinux) return PlatformKind.Linux;
            return PlatformKind.Unknown;
        }
    }
}

public enum PlatformKind
{
    Unknown,
    Windows,
    Linux,
    MacOS
}
