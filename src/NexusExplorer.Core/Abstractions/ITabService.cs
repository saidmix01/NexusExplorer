using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Abstraction for managing tabs in the file explorer.
/// At least one tab must always be open.
/// </summary>
public interface ITabService
{
    IReadOnlyList<TabItem> Tabs { get; }
    TabItem ActiveTab { get; }

    /// <summary>
    /// Creates a new tab at the specified path and activates it.
    /// </summary>
    TabItem CreateTab(string path, string? title = null);

    /// <summary>
    /// Adds an existing TabItem (e.g., restored from persisted state) and optionally activates it.
    /// </summary>
    void AddTab(TabItem tab, bool activate = false);

    /// <summary>
    /// Closes the specified tab. If it's the last tab, this is a no-op.
    /// If the closed tab was active, an adjacent tab is activated.
    /// </summary>
    void CloseTab(string tabId);

    /// <summary>
    /// Activates the specified tab.
    /// </summary>
    void ActivateTab(string tabId);

    /// <summary>
    /// Activates the next tab (wraps around).
    /// </summary>
    void ActivateNextTab();

    /// <summary>
    /// Activates the previous tab (wraps around).
    /// </summary>
    void ActivatePreviousTab();

    /// <summary>
    /// Moves a tab from one index to another (for drag-and-drop reordering).
    /// </summary>
    void MoveTab(int fromIndex, int toIndex);

    /// <summary>
    /// Fired when the tab collection changes (add/remove).
    /// </summary>
    event EventHandler? TabsChanged;

    /// <summary>
    /// Fired when the active tab changes. Includes the newly active tab.
    /// </summary>
    event EventHandler<TabItem>? ActiveTabChanged;
}
