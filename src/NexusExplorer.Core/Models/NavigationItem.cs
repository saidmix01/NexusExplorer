namespace NexusExplorer.Core.Models;

/// <summary>
/// Represents a navigation entry in the sidebar (e.g., quick access, favorites, drives).
/// Can also be a section header when IsSectionHeader is true.
/// </summary>
public sealed class NavigationItem
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string? Icon { get; init; }
    public NavigationItemKind Kind { get; init; }

    /// <summary>
    /// The sidebar section this item belongs to (FAVORITES, LOCATIONS, NETWORK).
    /// </summary>
    public NavigationSection Section { get; init; } = NavigationSection.Favorites;

    /// <summary>
    /// When true, this item is rendered as a section header, not a clickable nav item.
    /// </summary>
    public bool IsSectionHeader { get; init; }

    /// <summary>
    /// Whether the target path still exists. Custom group items pointing to a missing
    /// path are shown dimmed so they can be removed without throwing.
    /// </summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>
    /// Optional color hex (e.g. "#4A9FDE") used to render a color swatch for color-group
    /// sidebar entries. Null for ordinary navigation items.
    /// </summary>
    public string? ColorHex { get; init; }
}

public enum NavigationItemKind
{
    QuickAccess,
    Drive,
    Favorite,
    Network,
    SpecialLocation,
    Custom
}

public enum NavigationSection
{
    Favorites,
    Locations,
    Network
}
