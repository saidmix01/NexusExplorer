using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Win32;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Handles registering/unregistering NexusExplorer as the default file explorer on Windows.
/// Modifies HKCU registry keys to replace Explorer.exe shell behavior.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DefaultExplorerService
{
    private const string ShellFoldersKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string FileExplorerKey = @"SOFTWARE\Classes\Folder\shell\open\command";
    private const string DirectoryKey = @"SOFTWARE\Classes\Directory\shell\open\command";
    private const string DriveKey = @"SOFTWARE\Classes\Drive\shell\open\command";

    /// <summary>
    /// Checks whether NexusExplorer is currently set as the default folder handler.
    /// </summary>
    public static bool IsDefault()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(FileExplorerKey);
            var value = key?.GetValue("") as string;
            return value is not null && value.Contains("NexusExplorer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Registers NexusExplorer as the default file explorer.
    /// This sets HKCU registry entries so folders open with NexusExplorer instead of Explorer.exe.
    /// </summary>
    /// <param name="exePath">Full path to NexusExplorer.App.exe</param>
    /// <returns>True if registration succeeded.</returns>
    public static bool Register(string? exePath = null)
    {
        exePath ??= GetCurrentExePath();
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return false;

        var command = $"\"{exePath}\" \"%1\"";

        try
        {
            // Register for Folder\shell\open\command
            SetRegistryCommand(FileExplorerKey, command);

            // Register for Directory\shell\open\command
            SetRegistryCommand(DirectoryKey, command);

            // Register for Drive\shell\open\command
            SetRegistryCommand(DriveKey, command);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Unregisters NexusExplorer and restores the default Windows Explorer behavior.
    /// </summary>
    /// <returns>True if unregistration succeeded.</returns>
    public static bool Unregister()
    {
        try
        {
            // Delete custom keys to restore default Explorer.exe behavior
            DeleteRegistryCommand(FileExplorerKey);
            DeleteRegistryCommand(DirectoryKey);
            DeleteRegistryCommand(DriveKey);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the current process is running with elevated (admin) privileges.
    /// Note: HKCU keys don't require elevation, but this is useful for informational purposes.
    /// </summary>
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void SetRegistryCommand(string keyPath, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key?.SetValue("", command);
    }

    private static void DeleteRegistryCommand(string keyPath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
        catch
        {
            // Key might not exist — that's fine
        }
    }

    private static string? GetCurrentExePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            return processPath;

        // Fallback: try to find it relative to the app base directory
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "NexusExplorer.App.exe");
        return File.Exists(candidate) ? candidate : null;
    }
}
