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
        Process.Start(new ProcessStartInfo
        {
            FileName = "open",
            Arguments = $"\"{path}\"",
            UseShellExecute = false
        });
        return Task.CompletedTask;
    }

    public Task OpenWithDialogAsync(string path, CancellationToken cancellationToken = default)
    {
        // macOS: open -a prompts app selection if no default is set
        Process.Start(new ProcessStartInfo
        {
            FileName = "open",
            Arguments = $"\"{path}\"",
            UseShellExecute = false
        });
        return Task.CompletedTask;
    }

    public Task ShowInSystemExplorerAsync(string path, CancellationToken cancellationToken = default)
    {
        // macOS: open -R reveals the file in Finder
        Process.Start(new ProcessStartInfo
        {
            FileName = "open",
            Arguments = $"-R \"{path}\"",
            UseShellExecute = false
        });
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
