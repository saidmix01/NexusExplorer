namespace NexusExplorer.App.Resources;

/// <summary>
/// Centralized string resources for UI text.
/// This provides a simple pattern for localization: replace hardcoded strings
/// with references to this class, then add .resx files for each supported locale.
/// 
/// Current implementation: English strings as constants.
/// Future: Replace with IStringLocalizer&lt;T&gt; or .resx resource lookups.
/// </summary>
public static class Strings
{
    // === Window / Title Bar ===
    public const string AppName = "Nexus Explorer";
    public const string Close = "Close";
    public const string Minimize = "Minimize";
    public const string Maximize = "Maximize";
    public const string NewTab = "New Tab";

    // === Navigation ===
    public const string Back = "Back";
    public const string Forward = "Forward";
    public const string GoUp = "Go Up";
    public const string Refresh = "Refresh";

    // === Views ===
    public const string Icons = "Icons";
    public const string List = "List";
    public const string Details = "Details";
    public const string Tiles = "Tiles";

    // === Toolbar ===
    public const string Actions = "Actions";
    public const string Search = "Search";
    public const string ExplorerOnly = "Explorer Only";
    public const string SplitView = "Split View";
    public const string TerminalOnly = "Terminal Only";
    public const string TogglePreview = "Toggle Preview";
    public const string ToggleTheme = "Toggle Dark/Light Mode";

    // === File Operations ===
    public const string Open = "Open";
    public const string OpenWith = "Open With...";
    public const string OpenInNewTab = "Open in New Tab";
    public const string OpenInTerminal = "Open in Terminal";
    public const string ShowInExplorer = "Show in Explorer";
    public const string Cut = "Cut";
    public const string Copy = "Copy";
    public const string CopyPath = "Copy Path";
    public const string Paste = "Paste";
    public const string Rename = "Rename";
    public const string Delete = "Delete";
    public const string Compress = "Compress";
    public const string ExtractHere = "Extract Here";
    public const string SendTo = "Send To";
    public const string PinToQuickAccess = "Pin to Quick Access";
    public const string Properties = "Properties";
    public const string Duplicate = "Duplicate";

    // === Sort / Group ===
    public const string SortByName = "Sort by Name";
    public const string SortByDate = "Sort by Date";
    public const string SortBySize = "Sort by Size";
    public const string SortByType = "Sort by Type";
    public const string GroupByNone = "None";

    // === Dialogs ===
    public const string ConfirmDelete = "Confirm Delete";
    public const string Cancel = "Cancel";
    public const string Create = "Create";
    public const string NewFolder = "New Folder";
    public const string NewFile = "New File";

    // === Status Bar ===
    public const string Ready = "Ready";
    public const string ItemsFormat = "{0} items";
    public const string SearchingStatus = "Searching...";
    public const string NoItemsFound = "No items found";
    public const string FolderIsEmpty = "This folder is empty";

    // === Errors ===
    public const string AccessDenied = "Access denied";
    public const string FolderNotFound = "Folder not found";
    public const string ErrorGeneric = "Error";
    public const string PathNotFound = "Path not found: {0}";

    // === Terminal ===
    public const string TerminalNotSupported = "Terminal is not supported on this platform.";
    public const string NoShellFound = "No supported shell found.";

    // === Appearance ===
    public const string Light = "Light";
    public const string Dark = "Dark";
    public const string System = "System";
    public const string Appearance = "Appearance";

    // === Sidebar ===
    public const string Favorites = "FAVORITES";
    public const string Devices = "DEVICES";
    public const string Network = "NETWORK";
}
