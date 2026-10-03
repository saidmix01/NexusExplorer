using System.Diagnostics;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.MacOS;

/// <summary>
/// macOS-specific platform service implementation.
/// </summary>
public sealed class MacOSPlatformService : IPlatformService
{
    public string PlatformName => "macOS";

    public string HomePath => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public char PathSeparator => '/';

    public Task OpenWithDefaultAsync(string path, CancellationToken cancellationToken = default)
    {
        // With UseShellExecute=false, .NET on Unix does not strip quotes; pass args via ArgumentList.
        var psi = new ProcessStartInfo { FileName = "open", UseShellExecute = false };
        psi.ArgumentList.Add(path);
        Process.Start(psi);
        return Task.CompletedTask;
    }

    public Task OpenWithDialogAsync(string path, CancellationToken cancellationToken = default)
    {
        // macOS: open -a prompts app selection if no default is set
        var psi = new ProcessStartInfo { FileName = "open", UseShellExecute = false };
        psi.ArgumentList.Add(path);
        Process.Start(psi);
        return Task.CompletedTask;
    }

    public Task ShowInSystemExplorerAsync(string path, CancellationToken cancellationToken = default)
    {
        // macOS: open -R reveals the file in Finder
        var psi = new ProcessStartInfo { FileName = "open", UseShellExecute = false };
        psi.ArgumentList.Add("-R");
        psi.ArgumentList.Add(path);
        Process.Start(psi);
        return Task.CompletedTask;
    }

    public Task ShowPropertiesAsync(string path, CancellationToken cancellationToken = default)
    {
        // There is no standard native properties dialog on macOS; no-op.
        return Task.CompletedTask;
    }

    public IReadOnlyList<NavigationItem> GetQuickAccessFolders()
    {
        var folders = new List<NavigationItem>();
        var home = HomePath;

        AddIfExists(folders, "Home", home);
        AddIfExists(folders, "Desktop", Path.Combine(home, "Desktop"));
        AddIfExists(folders, "Documents", Path.Combine(home, "Documents"));
        AddIfExists(folders, "Downloads", Path.Combine(home, "Downloads"));
        AddIfExists(folders, "Pictures", Path.Combine(home, "Pictures"));
        AddIfExists(folders, "Videos", Path.Combine(home, "Movies"));
        AddIfExists(folders, "Music", Path.Combine(home, "Music"));

        return folders.AsReadOnly();
    }

    private static void AddIfExists(List<NavigationItem> list, string name, string path)
    {
        if (Directory.Exists(path))
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
