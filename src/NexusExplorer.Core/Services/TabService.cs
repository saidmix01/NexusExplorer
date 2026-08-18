using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Services;

/// <summary>
/// Manages the tab collection for the file explorer.
/// Guarantees at least one tab is always open.
/// </summary>
public sealed class TabService : ITabService
{
    private readonly ILogger<TabService> _logger;
    private readonly List<TabItem> _tabs = [];
    private TabItem _activeTab = null!;

    public TabService(ILogger<TabService>? logger = null)
    {
        _logger = logger ?? NullLogger<TabService>.Instance;
    }

    public IReadOnlyList<TabItem> Tabs => _tabs.AsReadOnly();
    public TabItem ActiveTab => _activeTab;

    public event EventHandler? TabsChanged;
    public event EventHandler<TabItem>? ActiveTabChanged;

    public TabItem CreateTab(string path, string? title = null)
    {
        var tab = new TabItem(path, title);
        _logger.LogDebug("Creating tab: {Path}", path);

        // Deactivate current active tab
        if (_activeTab is not null)
            _activeTab.IsActive = false;

        tab.IsActive = true;
        _tabs.Add(tab);
        _activeTab = tab;

        TabsChanged?.Invoke(this, EventArgs.Empty);
        ActiveTabChanged?.Invoke(this, tab);
        return tab;
    }

    public void AddTab(TabItem tab, bool activate = false)
    {
        _tabs.Add(tab);

        if (activate || _activeTab is null)
        {
            if (_activeTab is not null)
                _activeTab.IsActive = false;

            tab.IsActive = true;
            _activeTab = tab;
            ActiveTabChanged?.Invoke(this, tab);
        }
        else
        {
            tab.IsActive = false;
        }

        TabsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CloseTab(string tabId)
    {
        // Never close the last tab
        if (_tabs.Count <= 1) return;

        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab is null) return;

        _logger.LogDebug("Closing tab: {Path}", tab.CurrentPath);

        var index = _tabs.IndexOf(tab);
        tab.IsActive = false;
        _tabs.Remove(tab);

        if (_activeTab.Id == tabId)
        {
            // Activate adjacent tab: prefer right neighbor, fall back to left
            var newIndex = Math.Min(index, _tabs.Count - 1);
            _activeTab = _tabs[newIndex];
            _activeTab.IsActive = true;
            ActiveTabChanged?.Invoke(this, _activeTab);
        }

        TabsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ActivateTab(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab is null || tab.Id == _activeTab.Id) return;

        _activeTab.IsActive = false;
        _activeTab = tab;
        _activeTab.IsActive = true;
        ActiveTabChanged?.Invoke(this, _activeTab);
    }

    public void ActivateNextTab()
    {
        if (_tabs.Count <= 1) return;

        var currentIndex = _tabs.IndexOf(_activeTab);
        var nextIndex = (currentIndex + 1) % _tabs.Count;
        ActivateTab(_tabs[nextIndex].Id);
    }

    public void ActivatePreviousTab()
    {
        if (_tabs.Count <= 1) return;

        var currentIndex = _tabs.IndexOf(_activeTab);
        var prevIndex = (currentIndex - 1 + _tabs.Count) % _tabs.Count;
        ActivateTab(_tabs[prevIndex].Id);
    }

    public void MoveTab(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _tabs.Count) return;
        if (toIndex < 0 || toIndex >= _tabs.Count) return;
        if (fromIndex == toIndex) return;

        var tab = _tabs[fromIndex];
        _tabs.RemoveAt(fromIndex);
        _tabs.Insert(toIndex, tab);
        TabsChanged?.Invoke(this, EventArgs.Empty);
    }
}
