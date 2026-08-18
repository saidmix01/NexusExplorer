using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Abstraction for platform-specific operations.
/// </summary>
public interface IPlatformService
{
    /// <summary>
    /// Gets the name of the current platform (e.g., "Windows", "Linux", "macOS").
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Gets the default user home directory path.
    /// </summary>
    string HomePath { get; }

    /// <summary>
    /// Gets the platform-specific path separator character.
    /// </summary>
    char PathSeparator { get; }

    /// <summary>
    /// Opens a file or folder using the system default handler.
    /// </summary>
    Task OpenWithDefaultAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the system "Open With" dialog for the given file path,
    /// allowing the user to choose an application.
    /// </summary>
    Task OpenWithDialogAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the system file manager (Explorer/Finder/Nautilus) with the given file selected.
    /// </summary>
    Task ShowInSystemExplorerAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the native properties dialog for the given file or folder.
    /// </summary>
    Task ShowPropertiesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the standard user folders (Home, Desktop, Documents, Downloads, etc.)
    /// as navigation items for the sidebar.
    /// </summary>
    IReadOnlyList<NavigationItem> GetQuickAccessFolders();
}
