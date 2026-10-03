using System.Collections.ObjectModel;
using System.ComponentModel;

namespace NexusExplorer.Core.Models;

/// <summary>Well-known identifiers for the built-in sidebar groups.</summary>
public static class SidebarGroupIds
{
    public const string Favorites = "favorites";
    public const string Locations = "locations";
    public const string Network = "network";
    public const string Colors = "colors";
}

/// <summary>
/// A collapsible group in the sidebar (FAVORITES, LOCATIONS, NETWORK, or a user-created group).
/// </summary>
public sealed class SidebarGroup : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isExpanded = true;

    /// <summary>Stable identifier. System groups use "favorites", "locations", "network"; custom groups use a GUID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name shown in the group header.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <summary>True for the built-in groups that cannot be renamed, deleted, or reordered.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Whether the group's items are currently visible.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    /// <summary>The navigation items contained in this group, in user-defined order.</summary>
    public ObservableCollection<NavigationItem> Items { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
}
