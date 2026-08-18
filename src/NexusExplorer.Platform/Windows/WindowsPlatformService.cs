using System.Diagnostics;
using System.Runtime.InteropServices;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Windows-specific platform service implementation.
/// </summary>
public sealed class WindowsPlatformService : IPlatformService
{
    public string PlatformName => "Windows";

    public string HomePath => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public char PathSeparator => '\\';

    public Task OpenWithDefaultAsync(string path, CancellationToken cancellationToken = default)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    // --- Open With dialog via Win32 SHOpenWithDialog ---

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenAsInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string cszFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string cszClass;
        [MarshalAs(UnmanagedType.I4)] public OpenAsFlags oaifInFlags;
    }

    [Flags]
    private enum OpenAsFlags
    {
        OAIF_ALLOW_REGISTRATION = 0x00000001,
        OAIF_REGISTER_EXT = 0x00000002,
        OAIF_EXEC = 0x00000004,
        OAIF_FORCE_REGISTRATION = 0x00000008,
        OAIF_HIDE_REGISTRATION = 0x00000020,
        OAIF_URL_PROTOCOL = 0x00000040
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHOpenWithDialog(IntPtr hwndParent, ref OpenAsInfo poainfo);

    public Task OpenWithDialogAsync(string path, CancellationToken cancellationToken = default)
    {
        // Preferred: native SHOpenWithDialog (the real "How do you want to open this file?" dialog)
        return Task.Run(() =>
        {
            try
            {
                var info = new OpenAsInfo
                {
                    cszFile = path,
                    cszClass = string.Empty,
                    oaifInFlags = OpenAsFlags.OAIF_ALLOW_REGISTRATION
                                | OpenAsFlags.OAIF_EXEC
                };
                SHOpenWithDialog(IntPtr.Zero, ref info);
            }
            catch
            {
                // Fallback: rundll32 shell32.dll OpenAs_RunDLL
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = $"shell32.dll,OpenAs_RunDLL \"{path}\"",
                        UseShellExecute = false
                    });
                }
                catch { }
            }
        }, cancellationToken);
    }

    public Task ShowInSystemExplorerAsync(string path, CancellationToken cancellationToken = default)
    {
        // Open Explorer with the file selected
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = false
        });
        return Task.CompletedTask;
    }

    public Task ShowPropertiesAsync(string path, CancellationToken cancellationToken = default)
    {
        // Open the native Windows properties dialog for the item.
        _ = SHObjectProperties(IntPtr.Zero, SHOP_FILEPATH, path, null);
        return Task.CompletedTask;
    }

    private const uint SHOP_FILEPATH = 0x2;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHObjectProperties(
        IntPtr hwnd,
        uint shopObjectType,
        [MarshalAs(UnmanagedType.LPWStr)] string pszObjectName,
        [MarshalAs(UnmanagedType.LPWStr)] string? pszPropertyPage);

    public IReadOnlyList<NavigationItem> GetQuickAccessFolders()
    {
        var folders = new List<NavigationItem>();

        AddIfExists(folders, "Home", Environment.SpecialFolder.UserProfile);
        AddIfExists(folders, "Desktop", Environment.SpecialFolder.DesktopDirectory);
        AddIfExists(folders, "Documents", Environment.SpecialFolder.MyDocuments);
        AddIfExists(folders, "Downloads", null, Path.Combine(HomePath, "Downloads"));
        AddIfExists(folders, "Pictures", Environment.SpecialFolder.MyPictures);
        AddIfExists(folders, "Videos", Environment.SpecialFolder.MyVideos);
        AddIfExists(folders, "Music", Environment.SpecialFolder.MyMusic);

        return folders.AsReadOnly();
    }

    private static void AddIfExists(List<NavigationItem> list, string name, Environment.SpecialFolder? folder, string? fallbackPath = null)
    {
        var path = folder.HasValue
            ? Environment.GetFolderPath(folder.Value)
            : fallbackPath;

        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            list.Add(new NavigationItem
            {
                Name = name,
                Path = path,
                Kind = NavigationItemKind.QuickAccess,
                Section = NavigationSection.Favorites
            });
        }
    }
}
