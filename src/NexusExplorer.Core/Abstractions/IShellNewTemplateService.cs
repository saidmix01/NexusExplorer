using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Discovers the entries available for the context menu's "New" submenu, mirroring the OS shell's
/// own notion of what can be created in a folder. On Windows this reflects the registered
/// <c>ShellNew</c> associations (so Office/LibreOffice/etc. appear only when actually installed);
/// other platforms return a generic set.
/// </summary>
/// <remarks>
/// This is the single Windows-shell-integration seam the "New" feature depends on. It is defined
/// in Core (no OS types) and implemented in the Platform layer, so ViewModels and Views never touch
/// the registry or the shell directly. Implementations are expected to cache results and do their
/// work off the UI thread — the method is async specifically so the menu can be built without
/// blocking.
/// </remarks>
public interface IShellNewTemplateService
{
    /// <summary>
    /// Returns the available "New" items, built-in options first. Results are cached by the
    /// implementation; repeated calls are cheap and do not re-query the registry each time.
    /// Must never throw: a corrupt or unreadable association is skipped, not propagated.
    /// </summary>
    Task<IReadOnlyList<NewItemDefinition>> GetNewItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops any cached result so the next <see cref="GetNewItemsAsync"/> re-discovers entries.
    /// Called when the implementation (or the app) learns that file associations changed.
    /// </summary>
    void InvalidateCache();
}
