using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Win32;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Registers/unregisters NexusExplorer as the default handler for opening folders and drives
/// on Windows, in the same per-user way apps like OneCommander/FilePilot do.
///
/// Scope (Option A): intercepts the shell "open" verb for <c>Directory</c> and <c>Drive</c> so a
/// double-click on a folder or drive launches NexusExplorer instead of explorer.exe. It also
/// redefines the "This PC" shell folder's <c>opennewwindow</c> command so <c>Win+E</c> opens
/// NexusExplorer. All keys are written under <c>HKCU\Software\Classes</c>, so no administrator
/// rights are required and other users are unaffected.
///
/// Note: fully replacing explorer.exe (taskbar buttons, every internal shell dialog) is not
/// supported by Windows; this covers folder/drive double-click and Win+E, which is what the
/// popular third-party file managers achieve.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DefaultExplorerService
{
    // Per-user class registrations (no admin needed). These shadow the machine-wide HKCR entries.
    private const string DirectoryShell = @"Software\Classes\Directory\shell";
    private const string DirectoryOpenCommand = @"Software\Classes\Directory\shell\open\command";
    private const string DriveShell = @"Software\Classes\Drive\shell";
    private const string DriveOpenCommand = @"Software\Classes\Drive\shell\open\command";

    // The "This PC" shell folder CLSID — redefining its opennewwindow command captures Win+E.
    private const string ThisPcClsid = @"Software\Classes\CLSID\{52205fd8-5dfb-447d-801a-d0b52f2e83e1}";
    private const string ThisPcOpenNewWindowCommand = ThisPcClsid + @"\shell\opennewwindow\command";

    /// <summary>
    /// Checks whether NexusExplorer is currently registered as the folder/drive open handler.
    /// </summary>
    public static bool IsDefault()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DirectoryOpenCommand);
            var value = key?.GetValue("") as string;
            return value is not null && value.Contains("NexusExplorer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Registers NexusExplorer as the default folder/drive handler (and Win+E target).
    /// </summary>
    /// <param name="exePath">Full path to NexusExplorer.App.exe (auto-detected if null).</param>
    /// <returns>True if registration succeeded.</returns>
    public static bool Register(string? exePath = null)
    {
        exePath ??= GetCurrentExePath();
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return false;

        var openCommand = $"\"{exePath}\" \"%1\"";

        try
        {
            // Directory: force the "open" verb and point it at Nexus. Remove the stale "open"
            // subkey first so any inherited DelegateExecute/ddeexec (which Windows prioritizes
            // over our command and would otherwise re-launch explorer.exe) is cleared.
            DeleteSubKeyTree(DirectoryShell + @"\open");
            SetDefault(DirectoryShell, "open");
            SetDefault(DirectoryOpenCommand, openCommand);

            // Drive: same treatment.
            DeleteSubKeyTree(DriveShell + @"\open");
            SetDefault(DriveShell, "open");
            SetDefault(DriveOpenCommand, openCommand);

            // Win+E: redefine the "This PC" folder's opennewwindow command with an empty
            // DelegateExecute so the shell runs our exe instead of the built-in handler.
            SetDefault(ThisPcOpenNewWindowCommand, $"\"{exePath}\"");
            using (var cmdKey = Registry.CurrentUser.CreateSubKey(ThisPcOpenNewWindowCommand, writable: true))
                cmdKey?.SetValue("DelegateExecute", "");

            NotifyShell();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Unregisters NexusExplorer and restores the default Windows Explorer behavior by removing
    /// the per-user overrides (the machine-wide HKCR defaults then take effect again).
    /// </summary>
    public static bool Unregister()
    {
        try
        {
            DeleteSubKeyTree(DirectoryShell + @"\open");
            DeleteSubKeyTree(DriveShell + @"\open");
            // Restore the conventional default verb.
            SetDefault(DirectoryShell, "none");
            SetDefault(DriveShell, "none");

            DeleteSubKeyTree(ThisPcClsid);

            NotifyShell();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the current process is elevated. Not required for HKCU changes; informational only.
    /// </summary>
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void SetDefault(string keyPath, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key?.SetValue("", value);
    }

    private static void DeleteSubKeyTree(string keyPath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
        catch
        {
            // Missing key is fine.
        }
    }

    /// <summary>Tells the shell that file associations changed so the new handler is picked up.</summary>
    private static void NotifyShell()
    {
        try
        {
            NativeShellNotify.SHChangeNotify(
                NativeShellNotify.SHCNE_ASSOCCHANGED,
                NativeShellNotify.SHCNF_IDLIST,
                IntPtr.Zero,
                IntPtr.Zero);
        }
        catch
        {
            // Best effort — associations still take effect, just possibly after a shell restart.
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

[SupportedOSPlatform("windows")]
internal static class NativeShellNotify
{
    internal const uint SHCNE_ASSOCCHANGED = 0x08000000;
    internal const uint SHCNF_IDLIST = 0x0000;

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    internal static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
