namespace NexusExplorer.Core.Models;

/// <summary>
/// Represents the full persisted application state.
/// Serialized to JSON on shutdown and restored on startup.
/// </summary>
public sealed class AppState
{
    /// <summary>Window bounds and state.</summary>
    public WindowBoundsState Window { get; set; } = new();

    /// <summary>Open tabs with their individual state.</summary>
    public List<TabState> Tabs { get; set; } = [];

    /// <summary>The ID/index of the active tab.</summary>
    public int ActiveTabIndex { get; set; }

    /// <summary>Global preferences that are not per-tab.</summary>
    public UserPreferences Preferences { get; set; } = new();

    /// <summary>Sidebar groups (system + custom), persisted across sessions.</summary>
    public List<SidebarGroupState> SidebarGroups { get; set; } = [];
}

/// <summary>
/// Persisted window size, position, and state.
/// </summary>
public sealed class WindowBoundsState
{
    public double Width { get; set; } = 1100;
    public double Height { get; set; } = 720;
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public bool IsMaximized { get; set; }
}

/// <summary>
/// Persisted state for a single tab.
/// </summary>
public sealed class TabState
{
    public string CurrentPath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ExplorerViewMode ViewMode { get; set; } = ExplorerViewMode.Details;
    public LayoutMode LayoutMode { get; set; } = LayoutMode.ExplorerOnly;
    public SplitOrientation SplitOrientation { get; set; } = SplitOrientation.Horizontal;
    public double SplitRatio { get; set; } = 0.6;
    public FileSortMode SortMode { get; set; } = FileSortMode.Name;
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;
    public FileGroupMode GroupMode { get; set; } = FileGroupMode.None;
    public bool ShowHiddenFiles { get; set; }
    public bool IsTerminalOpen { get; set; }
    public List<string> BackStack { get; set; } = [];
    public List<string> ForwardStack { get; set; } = [];
}

/// <summary>
/// Global user preferences not tied to any specific tab.
/// </summary>
public sealed class UserPreferences
{
    public bool ShowPreviewPanel { get; set; }
    public double IconZoomLevel { get; set; } = 50;
    public List<string> PinnedFavorites { get; set; } = [];
    public ThemeMode Theme { get; set; } = ThemeMode.Light;
}

/// <summary>
/// Persisted state for a sidebar group. System groups are rebuilt on startup
/// and only persist their expanded/collapsed state; custom groups persist their items too.
/// </summary>
public sealed class SidebarGroupState
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public bool IsExpanded { get; set; } = true;
    public List<SidebarItemState> Items { get; set; } = [];
}

/// <summary>
/// Persisted state for a single item inside a custom sidebar group.
/// </summary>
public sealed class SidebarItemState
{
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
