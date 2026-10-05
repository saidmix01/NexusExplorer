using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Enables/disables launching Nexus Explorer when the user signs in to Windows, using the
/// per-user Run key (HKCU\Software\Microsoft\Windows\CurrentVersion\Run). No admin needed.
/// </summary>
[SupportedOSPlatform("windows")]
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NexusExplorer";

    /// <summary>
    /// Whether this feature is usable. Registry Run keys don't apply to MSIX/Store builds
    /// (those must use a StartupTask manifest extension instead).
    /// </summary>
    public static bool IsSupported => OperatingSystem.IsWindows() && !PackageInfo.IsPackaged;

    /// <summary>True if Nexus Explorer is currently set to run at login.</summary>
    public static bool IsEnabled()
    {
        if (!IsSupported) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value)
                && value.Contains("NexusExplorer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Enables or disables run-at-login. Returns true on success.</summary>
    public static bool SetEnabled(bool enabled)
    {
        if (!IsSupported) return false;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return false;

            if (enabled)
            {
                var exePath = GetCurrentExePath();
                if (string.IsNullOrEmpty(exePath)) return false;
                // Launch minimized to tray so sign-in isn't interrupted by a window popping up.
                key.SetValue(ValueName, $"\"{exePath}\" --startup");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetCurrentExePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            return processPath;

        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "NexusExplorer.App.exe");
        return File.Exists(candidate) ? candidate : null;
    }
}
