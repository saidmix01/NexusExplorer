using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.App.Services;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFileSystemService _fileSystemService;
    private readonly ITabService _tabService;
    private readonly IPlatformService _platformService;
    private readonly IPreviewService _previewService;
    private readonly IFileOperationService _fileOpService;
    private readonly IClipboardService _clipboardService;
    private readonly ISearchService _searchService;
    private readonly IOperationHistoryService _historyService;
    private readonly ISendToService _sendToService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly IStatePersistenceService _statePersistence;
    private readonly IFolderColorService _folderColorService;
    private readonly IFilePropertiesService _filePropertiesService;
    private readonly IFileOperationManager _fileOperationManager;
    private readonly IEditorService _editorService;
    private readonly ITerminalDiscoveryService _terminalDiscovery;
    private readonly ITerminalLauncher _terminalLauncher;
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly ILogger<MainWindowViewModel> _logger;

    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _searchCts;
    private readonly List<string> _pinnedFavorites = [];

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private string _addressBarText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private FileSystemItem? _selectedItem;

    [ObservableProperty]
    private bool _isPreviewVisible;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isAddressBarEditing;

    // --- Preview state ---

    [ObservableProperty]
    private bool _isPreviewLoading;

    [ObservableProperty]
    private PreviewType _previewType;

    [ObservableProperty]
    private string? _previewTextContent;

    [ObservableProperty]
    private string? _previewImagePath;

    [ObservableProperty]
    private string? _previewFileName;

    [ObservableProperty]
    private string? _previewFileType;

    [ObservableProperty]
    private long? _previewFileSize;

    [ObservableProperty]
    private DateTime? _previewLastModified;

    [ObservableProperty]
    private string? _previewFullPath;

    [ObservableProperty]
    private int? _previewImageWidth;

    [ObservableProperty]
    private int? _previewImageHeight;

    [ObservableProperty]
    private bool _hasPreviewError;

    [ObservableProperty]
    private string? _previewErrorMessage;

    // --- Layout state ---

    [ObservableProperty]
    private LayoutMode _layoutMode = LayoutMode.ExplorerOnly;

    [ObservableProperty]
    private SplitOrientation _splitOrientation = SplitOrientation.Horizontal;

    private double _splitRatio = 0.6;

    /// <summary>
    /// The proportion of the split devoted to the first pane (Explorer).
    /// Clamped to [0.2, 0.8].
    /// </summary>
    public double SplitRatio
    {
        get => _splitRatio;
        set
        {
            var clamped = Math.Clamp(value, 0.2, 0.8);
            if (Math.Abs(_splitRatio - clamped) < 0.001) return;
            _splitRatio = clamped;
            OnPropertyChanged();
            if (_tabService?.ActiveTab is not null)
                _tabService.ActiveTab.SplitRatio = _splitRatio;
        }
    }

    [ObservableProperty]
    private bool _isExplorerVisible = true;

    [ObservableProperty]
    private bool _isTerminalVisible;

    // --- File operation state ---

    [ObservableProperty]
    private bool _isOperationInProgress;

    [ObservableProperty]
    private string? _operationTitle;

    [ObservableProperty]
    private string? _operationStatusText;

    [ObservableProperty]
    private string? _operationSpeedText;

    [ObservableProperty]
    private double _operationProgress;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private bool _isDeleteConfirmationVisible;

    [ObservableProperty]
    private string _deleteConfirmationMessage = string.Empty;

    // --- File Operation Center state ---

    [ObservableProperty]
    private bool _isFileOperationCenterVisible;

    [ObservableProperty]
    private bool _isCloseConfirmationVisible;

    [ObservableProperty]
    private string _closeConfirmationMessage = string.Empty;

    private bool _fileOperationsWereActive;

    public ObservableCollection<FileOperation> Operations =>
        _fileOperationManager?.Operations ?? EmptyOperations;

    public bool HasActiveOperations =>
        _fileOperationManager?.HasActiveOperations ?? false;

    public bool IsOperationIndicatorVisible => HasActiveOperations && !IsFileOperationCenterVisible;

    public double OverallProgress
    {
        get
        {
            var active = GetActiveOperations();
            if (active.Count == 0) return 0;
            return active.Average(o => o.ProgressPercentage);
        }
    }

    public string OperationSummaryText
    {
        get
        {
            var active = GetActiveOperations();
            if (active.Count == 0) return string.Empty;

            var percent = active.Average(o => o.ProgressPercentage);
            return active.Count == 1
                ? $"1 operation in progress · {percent:F0}%"
                : $"{active.Count} operations in progress · {percent:F0}%";
        }
    }

    private List<FileOperation> GetActiveOperations()
    {
        if (_fileOperationManager is null) return [];

        return _fileOperationManager.Operations
            .Where(o => o.Status is FileOperationStatus.Pending or FileOperationStatus.Running)
            .ToList();
    }

    /// <summary>
    /// Raised after active operations have been cancelled so the App can shut down.
    /// </summary>
    public event Action? ApplicationExitRequested;

    private static readonly ObservableCollection<FileOperation> EmptyOperations = new();

    // --- Create Folder/File dialog state ---

    [ObservableProperty]
    private bool _isCreateDialogVisible;

    [ObservableProperty]
    private string _createDialogTitle = "New Folder";

    [ObservableProperty]
    private string _createDialogName = "New Folder";

    [ObservableProperty]
    private string _createDialogError = string.Empty;

    private bool _isCreateDialogForFolder = true;

    // --- Sidebar group dialog state ---

    [ObservableProperty]
    private bool _isGroupDialogVisible;

    [ObservableProperty]
    private string _groupDialogTitle = "New Group";

    [ObservableProperty]
    private string _groupDialogName = string.Empty;

    [ObservableProperty]
    private string _groupDialogError = string.Empty;

    [ObservableProperty]
    private string _groupDialogConfirmText = "Create";

    private SidebarGroup? _groupDialogTarget;

    // --- Move To / Copy To dialog ---

    [ObservableProperty]
    private bool _isMoveToDialogVisible;

    [ObservableProperty]
    private string _moveToCurrentPath = string.Empty;

    [ObservableProperty]
    private string _moveToDialogTitle = "Move To";

    public ObservableCollection<FileSystemItem> MoveToItems { get; } = [];

    private bool _isMoveToModeCopy;

    [ObservableProperty]
    private bool _isConflictResolutionVisible;

    [ObservableProperty]
    private string _conflictMessage = string.Empty;

    [ObservableProperty]
    private string _conflictSourceInfo = string.Empty;

    [ObservableProperty]
    private string _conflictDestinationInfo = string.Empty;

    [ObservableProperty]
    private bool _applyConflictToAll;

    private TaskCompletionSource<ConflictAction>? _conflictTcs;
    private ConflictAction? _applyToAllAction;

    [ObservableProperty]
    private ExplorerViewMode _viewMode = ExplorerViewMode.Details;

    private bool _isPermanentDelete;

    public bool HasClipboardContent => _clipboardService?.HasContent ?? false;
    public int ClipboardCount => _clipboardService?.Count ?? 0;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearchActive;

    [ObservableProperty]
    private FileSortMode _sortMode = FileSortMode.Name;

    [ObservableProperty]
    private SortDirection _sortDirection = SortDirection.Ascending;

    [ObservableProperty]
    private FileGroupMode _groupMode = FileGroupMode.None;

    [ObservableProperty]
    private bool _showHiddenFiles = false;

    [ObservableProperty]
    private bool _showItemInfo = false;

    [ObservableProperty]
    private ThemeMode _currentTheme = ThemeMode.Light;

    [ObservableProperty]
    private double _iconZoomLevel = 50;

    // Computed icon dimensions based on ViewMode + IconZoomLevel
    public int ComputedIconSize => ComputeIconSize();
    public double ComputedItemWidth => ComputeItemWidth();
    public double ComputedFontSize => ComputeFontSize();

    partial void OnIconZoomLevelChanged(double value)
    {
        OnPropertyChanged(nameof(ComputedIconSize));
        OnPropertyChanged(nameof(ComputedItemWidth));
        OnPropertyChanged(nameof(ComputedFontSize));
    }

    partial void OnIsFileOperationCenterVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsOperationIndicatorVisible));

    partial void OnCurrentThemeChanged(ThemeMode value)
    {
        ThemeService.Instance.ApplyTheme(value);

        // Force tab binding converters to re-evaluate with new theme colors.
        // Temporarily toggle IsActive to trigger PropertyChanged notification.
        foreach (var tab in Tabs)
        {
            var wasActive = tab.IsActive;
            tab.IsActive = !wasActive;
            tab.IsActive = wasActive;
            // Also re-notify FolderColor for the border converter
            var color = tab.FolderColor;
            tab.FolderColor = null;
            tab.FolderColor = color;
        }
    }

    partial void OnViewModeChanged(ExplorerViewMode value)
    {
        OnPropertyChanged(nameof(ComputedIconSize));
        OnPropertyChanged(nameof(ComputedItemWidth));
        OnPropertyChanged(nameof(ComputedFontSize));
    }

    private int ComputeIconSize()
    {
        double baseSize = ViewMode switch
        {
            ExplorerViewMode.ExtraLargeIcons => 96.0,
            ExplorerViewMode.LargeIcons => 64.0,
            ExplorerViewMode.MediumIcons => 48.0,
            ExplorerViewMode.SmallIcons => 32.0,
            _ => 48.0
        };
        var factor = 0.5 + (IconZoomLevel / 100.0) * 1.5;
        return (int)Math.Clamp(baseSize * factor, 16.0, 256.0);
    }

    private double ComputeItemWidth()
    {
        double baseWidth = ViewMode switch
        {
            ExplorerViewMode.ExtraLargeIcons => 140.0,
            ExplorerViewMode.LargeIcons => 100.0,
            ExplorerViewMode.MediumIcons => 80.0,
            ExplorerViewMode.SmallIcons => 60.0,
            _ => 80.0
        };
        var factor = 0.5 + (IconZoomLevel / 100.0) * 1.5;
        return Math.Clamp(baseWidth * factor, 50.0, 300.0);
    }

    private double ComputeFontSize()
    {
        double baseFontSize = ViewMode switch
        {
            ExplorerViewMode.ExtraLargeIcons => 13.0,
            ExplorerViewMode.LargeIcons => 12.0,
            ExplorerViewMode.MediumIcons => 11.0,
            ExplorerViewMode.SmallIcons => 10.0,
            _ => 11.0
        };
        var factor = 0.8 + (IconZoomLevel / 100.0) * 0.5;
        return Math.Clamp(baseFontSize * factor, 9.0, 18.0);
    }

    public ObservableCollection<FileSystemItem> SelectedItems { get; } = [];

    public TerminalViewModel Terminal { get; }

    public ObservableCollection<FileSystemItem> Items { get; } = [];
    public ObservableCollection<SidebarGroup> SidebarGroups { get; } = [];

    /// <summary>User-created (non-system) sidebar groups, used by the "Add to Sidebar Group" submenu.</summary>
    public IReadOnlyList<SidebarGroup> CustomGroups => SidebarGroups.Where(g => !g.IsSystem).ToList();

    public ObservableCollection<TabItem> Tabs { get; } = [];
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = [];

    public MainWindowViewModel(
        IFileSystemService fileSystemService,
        ITabService tabService,
        IPlatformService platformService,
        IPreviewService previewService,
        ITerminalSessionService terminalSessionService,
        IFileOperationService fileOpService,
        IClipboardService clipboardService,
        ISearchService searchService,
        IOperationHistoryService historyService,
        ICompressionService compressionService,
        ISendToService sendToService,
        IFileWatcherService fileWatcherService,
        IStatePersistenceService statePersistence,
        IFolderColorService folderColorService,
        IFilePropertiesService filePropertiesService,
        IFileOperationManager fileOperationManager,
        IEditorService editorService,
        ITerminalDiscoveryService terminalDiscovery,
        ITerminalLauncher terminalLauncher,
        IGlobalHotkeyService globalHotkeyService,
        ILogger<MainWindowViewModel>? logger = null)
    {
        StartupTiming.Mark("MainWindowViewModel ctor begin");
        _fileSystemService = fileSystemService;
        _tabService = tabService;
        _platformService = platformService;
        _previewService = previewService;
        _fileOpService = fileOpService;
        _clipboardService = clipboardService;
        _searchService = searchService;
        _historyService = historyService;
        // compressionService is now owned by the FileOperationManager; kept as a ctor param
        // for DI/signature stability but no longer stored on the view model.
        _ = compressionService;
        _sendToService = sendToService;
        _fileWatcherService = fileWatcherService;
        _statePersistence = statePersistence;
        _folderColorService = folderColorService;
        _filePropertiesService = filePropertiesService;
        _fileOperationManager = fileOperationManager;
        _fileOperationManager.Changed += OnFileOperationsChanged;
        _editorService = editorService;
        _terminalDiscovery = terminalDiscovery;
        _terminalLauncher = terminalLauncher;
        _globalHotkeyService = globalHotkeyService;
        _logger = logger ?? NullLogger<MainWindowViewModel>.Instance;
        _historyService.HistoryChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoDescription));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(RedoDescription));
        };
        _clipboardService.ClipboardChanged += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(HasClipboardContent));
                OnPropertyChanged(nameof(ClipboardCount));
            });
        };
        _fileWatcherService.Changed += OnFileWatcherChanged;
        Terminal = new TerminalViewModel(terminalSessionService);
        Terminal.TerminalDirectoryChanged += OnTerminalDirectoryChanged;
        Terminal.TerminalCloseRequested += OnTerminalCloseRequested;

        _tabService.TabsChanged += OnTabsChanged;
        _tabService.ActiveTabChanged += OnActiveTabChanged;

        SidebarGroups.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CustomGroups));

        LoadSidebarAsync();
        LoadFolderColorPresets();
        _ = LoadSecondaryDataAsync();

        // Restore session or create initial tab at home
        _ = RestoreSessionAsync();
        StartupTiming.Mark("MainWindowViewModel ctor end");
    }

    /// <summary>
    /// Loads non-critical startup data (installed editors and Send To targets)
    /// off the UI thread after the window is created. These are only needed when
    /// the user opens the "Open with" or "Send to" menus, so they must not delay
    /// the appearance of the main window.
    /// </summary>
    private async Task LoadSecondaryDataAsync()
    {
        try
        {
            var editors = await Task.Run(_editorService.GetInstalledEditors);
            foreach (var editor in editors)
                InstalledEditors.Add(editor);
        }
        catch
        {
            // Editor discovery is non-critical — ignore failures.
        }

        try
        {
            var targets = await Task.Run(_sendToService.GetTargets);
            foreach (var target in targets)
                SendToTargets.Add(target);
        }
        catch
        {
            // Send To discovery is non-critical — ignore failures.
        }

        try
        {
            var profiles = await Task.Run(_terminalDiscovery.Discover);
            foreach (var profile in profiles)
                AvailableTerminalProfiles.Add(profile);
        }
        catch (Exception ex)
        {
            // Terminal discovery is non-critical — the submenu just stays empty.
            _logger.LogWarning(ex, "Terminal discovery failed.");
        }
    }

    /// <summary>
    /// Design-time constructor.
    /// </summary>
    public MainWindowViewModel()
    {
        _fileSystemService = null!;
        _tabService = null!;
        _platformService = null!;
        _previewService = null!;
        _fileOpService = null!;
        _clipboardService = null!;
        _searchService = null!;
        _historyService = null!;
        _sendToService = null!;
        _fileWatcherService = null!;
        _statePersistence = null!;
        _folderColorService = null!;
        _filePropertiesService = null!;
        _fileOperationManager = null!;
        _editorService = null!;
        _terminalDiscovery = null!;
        _terminalLauncher = null!;
        _globalHotkeyService = null!;
        _logger = NullLogger<MainWindowViewModel>.Instance;
        Terminal = new TerminalViewModel();
    }

    public bool CanGoBack => _tabService?.ActiveTab?.CanGoBack ?? false;
    public bool CanGoForward => _tabService?.ActiveTab?.CanGoForward ?? false;
    public bool CanGoUp => _tabService?.ActiveTab?.CanGoUp ?? false;

    // --- Session persistence ---

    private async Task RestoreSessionAsync()
    {
        var launchPath = App.InitialLaunchPath;
        var hasLaunchPath = !string.IsNullOrWhiteSpace(launchPath) && Directory.Exists(launchPath);
        var restoredTabs = false;
        try
        {
            var state = await _statePersistence.LoadAsync();
            if (state is not null && state.Tabs.Count > 0)
            {
                restoredTabs = true;
                // Restore preferences
                IsPreviewVisible = state.Preferences.ShowPreviewPanel;
                IconZoomLevel = state.Preferences.IconZoomLevel;
                CurrentTheme = state.Preferences.Theme;

                if (!string.IsNullOrWhiteSpace(state.Preferences.GlobalHotkeyShortcut))
                    GlobalHotkeyShortcut = state.Preferences.GlobalHotkeyShortcut;
                GlobalHotkeyEnabled = state.Preferences.GlobalHotkeyEnabled;

                // Restore tabs
                for (int i = 0; i < state.Tabs.Count; i++)
                {
                    var ts = state.Tabs[i];
                    var path = Directory.Exists(ts.CurrentPath) ? ts.CurrentPath : _platformService.HomePath;
                    var tab = new TabItem(path, ts.Title, ts.BackStack, ts.ForwardStack)
                    {
                        ViewMode = ts.ViewMode,
                        LayoutMode = ts.LayoutMode,
                        SplitOrientation = ts.SplitOrientation,
                        SplitRatio = ts.SplitRatio,
                        SortMode = ts.SortMode,
                        SortDirection = ts.SortDirection,
                        GroupMode = ts.GroupMode,
                        ShowHiddenFiles = ts.ShowHiddenFiles,
                        IsTerminalOpen = ts.IsTerminalOpen
                    };
                    _tabService.AddTab(tab, activate: i == state.ActiveTabIndex);
                }
            }
            else if (!hasLaunchPath)
            {
                // No saved state and no launch path: create default Home tab.
                _tabService.CreateTab(_platformService.HomePath, "Home");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore session; falling back to default tab");
            // Fallback to default if restore fails (unless we have a launch path to open).
            if (_tabService.Tabs.Count == 0 && !hasLaunchPath)
                _tabService.CreateTab(_platformService.HomePath, "Home");
        }

        // If launched with a folder (e.g. "Reveal in Explorer" or a folder double-click):
        //  - if we restored/have existing tabs, open it in a NEW tab (keep the user's tabs);
        //  - if this is a fresh launch with no tabs yet, open it as the single tab (no extra Home).
        if (hasLaunchPath)
        {
            App.InitialLaunchPath = null;
            if (restoredTabs || _tabService.Tabs.Count > 0)
                OpenFolder(launchPath);
            else
            {
                _tabService.CreateTab(launchPath!);
                LoadDirectory(launchPath!);
            }
        }

        RegisterGlobalHotkeyOnStartup();
    }

    /// <summary>
    /// Opens the given folder in a NEW tab (used for external launches: shell "open"/"reveal",
    /// folder double-click, and single-instance activation). Creating a new tab preserves
    /// whatever the user was already looking at.
    /// </summary>
    [RelayCommand]
    private void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!Directory.Exists(path) && !VirtualPaths.IsVirtual(path)) return;

        // If this folder is already open in a tab, activate that tab instead of duplicating it.
        var existing = _tabService.Tabs.FirstOrDefault(t =>
            string.Equals(t.CurrentPath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _tabService.ActivateTab(existing.Id);
            return;
        }

        _tabService.CreateTab(path);
        LoadDirectory(path);
    }

    /// <summary>
    /// Saves the current session state. Called from the View on window closing.
    /// </summary>
    public async Task SaveSessionAsync(double windowWidth, double windowHeight, double windowX, double windowY, bool isMaximized)
    {
        var state = new AppState
        {
            Window = new WindowBoundsState
            {
                Width = windowWidth,
                Height = windowHeight,
                X = windowX,
                Y = windowY,
                IsMaximized = isMaximized
            },
            ActiveTabIndex = Math.Max(0, _tabService.Tabs.ToList().FindIndex(t => t.IsActive)),
            Preferences = new UserPreferences
            {
                ShowPreviewPanel = IsPreviewVisible,
                IconZoomLevel = IconZoomLevel,
                PinnedFavorites = _pinnedFavorites.ToList(),
                Theme = CurrentTheme,
                GlobalHotkeyEnabled = GlobalHotkeyEnabled,
                GlobalHotkeyShortcut = GlobalHotkeyShortcut
            },
            SidebarGroups = SidebarGroups.Select(g => new SidebarGroupState
            {
                Id = g.Id,
                Name = g.Name,
                IsSystem = g.IsSystem,
                IsExpanded = g.IsExpanded,
                Items = g.IsSystem
                    ? []
                    : g.Items.Select(i => new SidebarItemState
                    {
                        Path = i.Path,
                        DisplayName = i.Name
                    }).ToList()
            }).ToList()
        };

        foreach (var tab in _tabService.Tabs)
        {
            state.Tabs.Add(new TabState
            {
                CurrentPath = tab.CurrentPath,
                Title = tab.Title,
                ViewMode = tab.ViewMode,
                LayoutMode = tab.LayoutMode,
                SplitOrientation = tab.SplitOrientation,
                SplitRatio = tab.SplitRatio,
                SortMode = tab.SortMode,
                SortDirection = tab.SortDirection,
                GroupMode = tab.GroupMode,
                ShowHiddenFiles = tab.ShowHiddenFiles,
                IsTerminalOpen = tab.IsTerminalOpen,
                BackStack = tab.BackStackSnapshot.ToList(),
                ForwardStack = tab.ForwardStackSnapshot.ToList()
            });
        }

        await _statePersistence.SaveAsync(state);
    }

    /// <summary>
    /// Loads window bounds from persisted state. Called from the View on startup.
    /// </summary>
    public async Task<WindowBoundsState?> GetPersistedWindowStateAsync()
    {
        var state = await _statePersistence.LoadAsync();
        return state?.Window;
    }

    // --- Selection changed handler ---

    partial void OnSelectedItemChanged(FileSystemItem? value)
    {
        if (_tabService?.ActiveTab is not null)
            _tabService.ActiveTab.SelectedItem = value;

        if (IsPreviewVisible)
            LoadPreviewAsync(value);
    }

    partial void OnIsPreviewVisibleChanged(bool value)
    {
        if (value && SelectedItem is not null)
            LoadPreviewAsync(SelectedItem);
        else if (!value)
            ClearPreview();
    }

    // --- Navigation commands (operate on active tab) ---

    [RelayCommand]
    private void GoBack()
    {
        if (_tabService.ActiveTab is not { } tab) return;
        var path = tab.GoBack();
        if (path is not null)
            LoadDirectory(path);
    }

    [RelayCommand]
    private void GoForward()
    {
        if (_tabService.ActiveTab is not { } tab) return;
        var path = tab.GoForward();
        if (path is not null)
            LoadDirectory(path);
    }

    [RelayCommand]
    private void GoUp()
    {
        if (_tabService.ActiveTab is not { } tab) return;
        var path = tab.GoUp();
        if (path is not null)
            LoadDirectory(path);
    }

    [RelayCommand]
    private void Refresh()
    {
        if (_tabService.ActiveTab is not { } tab) return;
        var path = tab.Refresh();
        LoadDirectory(path);
    }

    /// <summary>
    /// Returns true if the active tab has a terminal session with a running process.
    /// Uses the TerminalViewModel's reactive IsSessionActive property to avoid
    /// calling native Win32 APIs on the UI thread.
    /// </summary>
    private bool IsCurrentTabTerminalBusy()
    {
        return Terminal.IsTerminalOpen && Terminal.IsSessionActive;
    }

    [RelayCommand]
    private void NavigateTo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _tabService.ActiveTab.NavigateTo(path);
        LoadDirectory(path);
    }

    [RelayCommand]
    private void NavigateToAddress(string? addressBarText)
    {
        var path = (addressBarText ?? AddressBarText).Trim();
        if (string.IsNullOrWhiteSpace(path))
            return;

        // Handle file:// URIs
        if (path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            try { path = new Uri(path).LocalPath; }
            catch { /* Keep original if URI parsing fails */ }
        }
        else if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try { path = new Uri(path).LocalPath; }
            catch { }
        }

        // Normalize path separators
        path = path.Replace('/', '\\');

        IsAddressBarEditing = false;

        if (Directory.Exists(path))
        {
            _tabService.ActiveTab.NavigateTo(path);
            LoadDirectory(path);
        }
        else if (File.Exists(path))
        {
            // If it's a file, navigate to its directory and open it
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                _tabService.ActiveTab.NavigateTo(dir);
                LoadDirectory(dir);
            }
            _ = _platformService.OpenWithDefaultAsync(path);
        }
        else
        {
            // Check if it's a potential UNC path or unmapped drive
            StatusText = $"Path not found: {path}";
        }
    }

    // --- Tab commands ---

    [RelayCommand]
    private void NewTab()
    {
        var path = _platformService.HomePath;
        _tabService.CreateTab(path);
    }

    [RelayCommand]
    private void NewTabAtPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _tabService.CreateTab(path);
    }

    [RelayCommand]
    private void CloseTab(string? tabId)
    {
        if (string.IsNullOrEmpty(tabId)) return;

        var tab = _tabService.Tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab is null) return;

        // If terminal is running, show confirmation
        // Use IsTerminalOpen + check IsSessionActive via Terminal ViewModel if it's the active tab
        // to avoid calling native IsRunning on the UI thread
        var isTerminalBusy = tab.IsTerminalOpen && (
            _tabService.ActiveTab.Id == tabId
                ? Terminal.IsSessionActive
                : tab.TerminalSession is not null);

        if (isTerminalBusy)
        {
            _pendingCloseTabId = tabId;
            IsTerminalCloseConfirmVisible = true;
            return;
        }

        Terminal.DisposeTabTerminal(tab);
        _tabService.CloseTab(tabId);
    }

    // --- Terminal close confirmation ---

    [ObservableProperty]
    private bool _isTerminalCloseConfirmVisible;

    private string? _pendingCloseTabId;

    [RelayCommand]
    private void ConfirmTerminalClose()
    {
        IsTerminalCloseConfirmVisible = false;
        if (_pendingCloseTabId is null) return;

        var tab = _tabService.Tabs.FirstOrDefault(t => t.Id == _pendingCloseTabId);
        if (tab is not null)
            Terminal.DisposeTabTerminal(tab);
        _tabService.CloseTab(_pendingCloseTabId);
        _pendingCloseTabId = null;
    }

    [RelayCommand]
    private void CancelTerminalClose()
    {
        IsTerminalCloseConfirmVisible = false;
        _pendingCloseTabId = null;
    }

    [RelayCommand]
    private void CloseActiveTab()
    {
        var tab = _tabService.ActiveTab;
        if (tab.IsTerminalOpen && Terminal.IsSessionActive)
        {
            _pendingCloseTabId = tab.Id;
            IsTerminalCloseConfirmVisible = true;
            return;
        }
        Terminal.DisposeTabTerminal(tab);
        _tabService.CloseTab(tab.Id);
    }

    [RelayCommand]
    private void DuplicateTab(string? tabId)
    {
        if (string.IsNullOrEmpty(tabId)) return;
        var source = _tabService.Tabs.FirstOrDefault(t => t.Id == tabId);
        if (source is null) return;
        _tabService.CreateTab(source.CurrentPath, source.Title);
    }

    [RelayCommand]
    private void CloseOtherTabs(string? tabId)
    {
        if (string.IsNullOrEmpty(tabId)) return;
        var idsToClose = _tabService.Tabs
            .Where(t => t.Id != tabId)
            .Select(t => t.Id)
            .ToList();
        foreach (var id in idsToClose)
        {
            var tab = _tabService.Tabs.FirstOrDefault(t => t.Id == id);
            if (tab is not null) Terminal.DisposeTabTerminal(tab);
            _tabService.CloseTab(id);
        }
    }

    [RelayCommand]
    private void CloseAllTabs()
    {
        // Close all except the last one (at least one must remain), then navigate to home
        var ids = _tabService.Tabs.Select(t => t.Id).ToList();
        for (int i = ids.Count - 1; i >= 1; i--)
        {
            var tab = _tabService.Tabs.FirstOrDefault(t => t.Id == ids[i]);
            if (tab is not null) Terminal.DisposeTabTerminal(tab);
            _tabService.CloseTab(ids[i]);
        }
        // Navigate the remaining tab to home
        var remaining = _tabService.ActiveTab;
        remaining.NavigateTo(_platformService.HomePath);
        LoadDirectory(_platformService.HomePath);
    }

    [RelayCommand]
    private void ActivateTab(string? tabId)
    {
        if (string.IsNullOrEmpty(tabId)) return;
        _tabService.ActivateTab(tabId);
    }

    [RelayCommand]
    private void NextTab()
    {
        _tabService.ActivateNextTab();
    }

    [RelayCommand]
    private void PreviousTab()
    {
        _tabService.ActivatePreviousTab();
    }

    [RelayCommand]
    private void MoveTab(int[]? indices)
    {
        if (indices is not { Length: 2 }) return;
        _tabService.MoveTab(indices[0], indices[1]);
    }

    // --- Terminal commands ---

    [RelayCommand]
    private async Task ToggleTerminalAsync()
    {
        if (LayoutMode == LayoutMode.ExplorerOnly)
            await SetLayoutModeAsync(LayoutMode.Split);
        else if (LayoutMode == LayoutMode.Split)
            await SetLayoutModeAsync(LayoutMode.ExplorerOnly);
        else
            await SetLayoutModeAsync(LayoutMode.Split);
    }

    [RelayCommand]
    private async Task SetExplorerOnlyAsync()
    {
        await SetLayoutModeAsync(LayoutMode.ExplorerOnly);
    }

    [RelayCommand]
    private async Task SetSplitAsync()
    {
        await SetLayoutModeAsync(LayoutMode.Split);
    }

    [RelayCommand]
    private async Task SetTerminalOnlyAsync()
    {
        await SetLayoutModeAsync(LayoutMode.TerminalOnly);
    }

    [RelayCommand]
    private void ToggleSplitOrientation()
    {
        SplitOrientation = SplitOrientation == SplitOrientation.Horizontal
            ? SplitOrientation.Vertical
            : SplitOrientation.Horizontal;

        if (_tabService?.ActiveTab is not null)
            _tabService.ActiveTab.SplitOrientation = SplitOrientation;

        // The code-behind listens to SplitOrientation changes via PropertyChanged
    }

    private async Task SetLayoutModeAsync(LayoutMode mode)
    {
        LayoutMode = mode;
        IsExplorerVisible = mode != LayoutMode.TerminalOnly;
        IsTerminalVisible = mode != LayoutMode.ExplorerOnly;

        if (_tabService?.ActiveTab is not null)
        {
            _tabService.ActiveTab.LayoutMode = mode;
            _tabService.ActiveTab.IsTerminalOpen = IsTerminalVisible;
        }

        // Lazily create the terminal session, or sync an existing session's directory.
        if (IsTerminalVisible)
        {
            if (!Terminal.IsSessionActive)
            {
                await Terminal.OpenTerminalAsync();
            }
            else if (_tabService?.ActiveTab is not null)
            {
                // A session is already running (e.g. the terminal was hidden and is being
                // shown again). Make sure it points at the folder currently being browsed.
                await Terminal.SyncWorkingDirectoryAsync(_tabService.ActiveTab.CurrentPath);
            }
        }
    }

    // --- Other commands ---

    [RelayCommand]
    private void TogglePreview()
    {
        IsPreviewVisible = !IsPreviewVisible;
    }

    // --- File operation commands ---

    [RelayCommand]
    private void CopySelected()
    {
        var paths = GetSelectedPaths();
        if (paths.Count > 0)
        {
            _clipboardService.SetCopy(paths);
            StatusText = $"{_clipboardService.Count} item(s) in clipboard";
        }
    }

    [RelayCommand]
    private void ClearClipboard()
    {
        _clipboardService.Clear();
        StatusText = "Clipboard cleared";
    }

    [RelayCommand]
    private void CopyPath()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;
        var text = string.Join(Environment.NewLine, paths);
        CopyTextToClipboardRequested?.Invoke(text);
        StatusText = paths.Count == 1 ? "Path copied" : $"{paths.Count} paths copied";
    }

    /// <summary>
    /// Fired when CopyPath needs to set text on the system clipboard.
    /// The View subscribes to this to perform the actual clipboard write.
    /// </summary>
    public event Action<string>? CopyTextToClipboardRequested;

    // --- Compress command ---

    [RelayCommand]
    private async Task CompressSelectedAsync()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;

        var title = paths.Count == 1
            ? $"Compressing '{Path.GetFileName(paths[0])}'"
            : $"Compressing {paths.Count} items";

        // Managed background operation (File Operation Center, survives tray, cancellable).
        var result = await _fileOperationManager.CompressAsync(paths, title);

        if (result.Success)
            StatusText = $"Created {Path.GetFileName(result.ArchivePath)}";
        else if (!result.Cancelled && result.Error is not null)
            StatusText = result.Error;

        await RefreshCurrentDirectoryAsync();
    }

    [RelayCommand]
    private async Task ExtractSelectedAsync()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;

        // Only extract ZIP files
        var archivePath = paths.FirstOrDefault(p =>
            p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        if (archivePath is null)
        {
            StatusText = "No ZIP file selected for extraction.";
            return;
        }

        var title = $"Extracting '{Path.GetFileName(archivePath)}'";

        // Suppress watcher refreshes during extraction to avoid list churn.
        using var _suppress = _fileWatcherService.Suppress();

        // Managed background operation (File Operation Center, survives tray, cancellable).
        var result = await _fileOperationManager.ExtractAsync(archivePath, null, title);

        if (result.Success)
            StatusText = $"Extracted to {Path.GetFileName(result.ArchivePath)}";
        else if (!result.Cancelled && result.Error is not null)
            StatusText = result.Error;

        await RefreshCurrentDirectoryAsync();
    }

    // --- WinRAR integration ---

    public bool IsWinRarAvailable =>
        OperatingSystem.IsWindows() && NexusExplorer.Platform.Windows.WinRarService.IsAvailable;

    [RelayCommand]
    private void OpenWithWinRar()
    {
        if (!OperatingSystem.IsWindows()) return;
        var item = SelectedItem;
        if (item is null) return;
        NexusExplorer.Platform.Windows.WinRarService.OpenInWinRar(item.Path);
    }

    [RelayCommand]
    private void ExtractHereWithWinRar()
    {
        if (!OperatingSystem.IsWindows()) return;
        var item = SelectedItem;
        if (item is null) return;
        NexusExplorer.Platform.Windows.WinRarService.ExtractHere(item.Path);
        _ = RefreshCurrentDirectoryAsync();
    }

    [RelayCommand]
    private void ExtractToFolderWithWinRar()
    {
        if (!OperatingSystem.IsWindows()) return;
        var item = SelectedItem;
        if (item is null) return;
        NexusExplorer.Platform.Windows.WinRarService.ExtractToFolder(item.Path);
        _ = RefreshCurrentDirectoryAsync();
    }

    [RelayCommand]
    private void AddToArchiveWithWinRar()
    {
        if (!OperatingSystem.IsWindows()) return;
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;
        NexusExplorer.Platform.Windows.WinRarService.AddToArchiveDialog(paths);
    }

    // --- Default Explorer registration ---

    public bool IsDefaultExplorer =>
        OperatingSystem.IsWindows() && NexusExplorer.Platform.Windows.DefaultExplorerService.IsDefault();

    [RelayCommand]
    private void SetAsDefaultExplorer()
    {
        if (!OperatingSystem.IsWindows()) { StatusText = "Only available on Windows"; return; }
        var success = NexusExplorer.Platform.Windows.DefaultExplorerService.Register();
        StatusText = success
            ? "NexusExplorer is now the default file explorer"
            : "Failed to register as default explorer";
        OnPropertyChanged(nameof(IsDefaultExplorer));
    }

    [RelayCommand]
    private void RestoreDefaultExplorer()
    {
        if (!OperatingSystem.IsWindows()) { StatusText = "Only available on Windows"; return; }
        var success = NexusExplorer.Platform.Windows.DefaultExplorerService.Unregister();
        StatusText = success
            ? "Windows Explorer restored as default"
            : "Failed to restore default explorer";
        OnPropertyChanged(nameof(IsDefaultExplorer));
    }

    // --- Open with editor ---

    public ObservableCollection<EditorInfo> InstalledEditors { get; } = [];

    /// <summary>External terminals detected on this system, shown under "Open in Other Terminal".</summary>
    public ObservableCollection<TerminalProfile> AvailableTerminalProfiles { get; } = [];

    // --- Global hotkey ---

    private const string GlobalHotkeyId = "open-nexus";
    private const string DefaultGlobalHotkey = "Ctrl+Alt+E";

    [ObservableProperty]
    private string _globalHotkeyShortcut = DefaultGlobalHotkey;

    [ObservableProperty]
    private bool _globalHotkeyEnabled = true;

    [ObservableProperty]
    private string _globalHotkeyStatus = string.Empty;

    [ObservableProperty]
    private bool _isGlobalHotkeyCapturing;

    public bool IsGlobalHotkeySupported => _globalHotkeyService?.IsSupported ?? false;

    /// <summary>Raised when the user requests to open the Settings dialog.</summary>
    public event Action? OpenSettingsRequested;

    [RelayCommand]
    private void OpenSettings() => OpenSettingsRequested?.Invoke();

    [RelayCommand]
    private void StartGlobalHotkeyCapture()
    {
        GlobalHotkeyStatus = "Press a new shortcut... (Esc to cancel)";
        IsGlobalHotkeyCapturing = true;
    }

    [RelayCommand]
    private void CancelGlobalHotkeyCapture()
    {
        IsGlobalHotkeyCapturing = false;
        if (GlobalHotkeyStatus == "Press a new shortcut... (Esc to cancel)")
            GlobalHotkeyStatus = string.Empty;
    }

    public async Task ApplyGlobalHotkeyShortcutAsync(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return;

        var previous = GlobalHotkeyShortcut;
        GlobalHotkeyShortcut = shortcut;

        if (TryRegisterCurrentHotkey())
        {
            GlobalHotkeyStatus = string.Empty;
            await SaveGlobalHotkeyConfigAsync();
        }
        else
        {
            // Roll back to the previous shortcut so the user is never left without one.
            GlobalHotkeyShortcut = previous;
            if (GlobalHotkeyEnabled)
                _ = _globalHotkeyService?.Register(new GlobalHotkey { Id = GlobalHotkeyId, Shortcut = previous });
            GlobalHotkeyStatus = "Shortcut unavailable. It is already registered by another application or Windows.";
        }
    }

    public async Task ApplyGlobalHotkeyEnabledAsync()
    {
        if (GlobalHotkeyEnabled)
        {
            if (!TryRegisterCurrentHotkey())
            {
                GlobalHotkeyEnabled = false;
                GlobalHotkeyStatus = "Shortcut unavailable. It is already registered by another application or Windows.";
            }
            else
            {
                GlobalHotkeyStatus = string.Empty;
            }
        }
        else
        {
            _globalHotkeyService?.Unregister(GlobalHotkeyId);
            GlobalHotkeyStatus = string.Empty;
        }

        await SaveGlobalHotkeyConfigAsync();
    }

    [RelayCommand]
    private async Task ResetGlobalHotkeyAsync()
    {
        await ApplyGlobalHotkeyShortcutAsync(DefaultGlobalHotkey);
    }

    private bool TryRegisterCurrentHotkey()
    {
        if (_globalHotkeyService is null || !_globalHotkeyService.IsSupported)
            return true; // nothing to register on unsupported platforms

        _globalHotkeyService.Unregister(GlobalHotkeyId);
        return _globalHotkeyService.Register(new GlobalHotkey
        {
            Id = GlobalHotkeyId,
            Enabled = GlobalHotkeyEnabled,
            Shortcut = GlobalHotkeyShortcut,
        });
    }

    private void RegisterGlobalHotkeyOnStartup()
    {
        if (!GlobalHotkeyEnabled) return;

        if (!TryRegisterCurrentHotkey())
            GlobalHotkeyStatus = "Shortcut unavailable. It is already registered by another application or Windows.";
    }

    private async Task SaveGlobalHotkeyConfigAsync()
    {
        try
        {
            var state = await _statePersistence.LoadAsync() ?? new AppState();
            state.Preferences.GlobalHotkeyEnabled = GlobalHotkeyEnabled;
            state.Preferences.GlobalHotkeyShortcut = GlobalHotkeyShortcut;
            await _statePersistence.SaveAsync(state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save global hotkey configuration.");
        }
    }

    [RelayCommand]
    private void OpenWithEditor(EditorInfo? editor)
    {
        if (editor is null) return;

        var folder = SelectedItem?.Path;
        if (string.IsNullOrWhiteSpace(folder) || SelectedItem?.Type != FileSystemItemType.Directory)
            folder = CurrentPath;
        if (string.IsNullOrWhiteSpace(folder)) return;

        try
        {
            _editorService.Open(editor, folder);
        }
        catch (Exception ex)
        {
            StatusText = $"Cannot open with {editor.Name}: {ex.Message}";
        }
    }

    // --- Send To commands ---

    public ObservableCollection<SendToTarget> SendToTargets { get; } = [];

    [RelayCommand]
    private void RefreshSendToTargets()
    {
        SendToTargets.Clear();
        var targets = _sendToService.GetTargets();
        foreach (var target in targets)
            SendToTargets.Add(target);
    }

    [RelayCommand]
    private async Task SendToAsync(SendToTarget? target)
    {
        if (target is null) return;
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;

        _applyToAllAction = null;
        ApplyConflictToAll = false;

        var title = paths.Count == 1
            ? $"Sending '{Path.GetFileName(paths[0])}' to {target.Name}"
            : $"Sending {paths.Count} items to {target.Name}";

        // Runs as a managed background operation (visible in the File Operation Center,
        // survives minimizing to tray, and cancellable from any tab).
        var result = await _fileOperationManager.SendToAsync(
            paths, target, title, conflictResolver: HandleConflictAsync);

        if (result.Success)
            StatusText = $"Sent {result.ItemsProcessed} item(s) to {target.Name}";
        else if (!result.Cancelled && result.Error is not null)
            StatusText = result.Error;

        await RefreshCurrentDirectoryAsync();
    }

    // --- Folder Color commands ---

    public ObservableCollection<FolderColorOption> FolderColorPresets { get; } = new();

    private void LoadFolderColorPresets()
    {
        FolderColorPresets.Clear();
        if (_folderColorService is null) return;
        foreach (var preset in _folderColorService.GetPresetColors())
            FolderColorPresets.Add(preset);
    }

    [RelayCommand]
    private void SetFolderColor(string? colorHex)
    {
        // Apply to all selected directories (or the single selected item as a fallback).
        var targets = SelectedItems.Count > 0
            ? SelectedItems.Where(i => i.Type == FileSystemItemType.Directory).ToList()
            : SelectedItem is { Type: FileSystemItemType.Directory } single ? [single] : [];
        if (targets.Count == 0) return;

        foreach (var folder in targets)
        {
            _folderColorService.SetColor(folder.Path, colorHex);
            if (string.Equals(folder.Path, CurrentPath, StringComparison.OrdinalIgnoreCase)
                && _tabService?.ActiveTab is { } tab)
                tab.FolderColor = colorHex;
        }
        RefreshColorGroups();
        LoadDirectory(CurrentPath);
    }

    [RelayCommand]
    private void RemoveFolderColor()
    {
        var targets = SelectedItems.Count > 0
            ? SelectedItems.Where(i => i.Type == FileSystemItemType.Directory).ToList()
            : SelectedItem is { Type: FileSystemItemType.Directory } single ? [single] : [];
        if (targets.Count == 0) return;

        foreach (var folder in targets)
        {
            _folderColorService.SetColor(folder.Path, null);
            if (string.Equals(folder.Path, CurrentPath, StringComparison.OrdinalIgnoreCase)
                && _tabService?.ActiveTab is { } tab)
                tab.FolderColor = null;
        }
        RefreshColorGroups();
        LoadDirectory(CurrentPath);
    }

    // --- Move To / Copy To dialog ---

    [RelayCommand]
    private void ShowMoveTo()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;
        _isMoveToModeCopy = false;
        MoveToDialogTitle = "Move To";
        MoveToCurrentPath = _platformService.HomePath;
        LoadMoveToItems(MoveToCurrentPath);
        IsMoveToDialogVisible = true;
    }

    [RelayCommand]
    private void ShowCopyTo()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;
        _isMoveToModeCopy = true;
        MoveToDialogTitle = "Copy To";
        MoveToCurrentPath = _platformService.HomePath;
        LoadMoveToItems(MoveToCurrentPath);
        IsMoveToDialogVisible = true;
    }

    [RelayCommand]
    private void MoveToNavigate(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!Directory.Exists(path)) return;
        MoveToCurrentPath = path;
        LoadMoveToItems(path);
    }

    [RelayCommand]
    private void MoveToGoUp()
    {
        var parent = Path.GetDirectoryName(MoveToCurrentPath);
        if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
        {
            MoveToCurrentPath = parent;
            LoadMoveToItems(parent);
        }
    }

    [RelayCommand]
    private async Task ConfirmMoveToAsync()
    {
        IsMoveToDialogVisible = false;
        var destination = MoveToCurrentPath;
        if (string.IsNullOrEmpty(destination) || !Directory.Exists(destination)) return;

        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;

        // Use SendToService to perform the actual copy/move
        var target = new SendToTarget { Name = Path.GetFileName(destination), Path = destination };
        if (_isMoveToModeCopy)
        {
            await _sendToService.SendToAsync(paths, target);
            StatusText = $"Copied {paths.Count} item(s) to {Path.GetFileName(destination)}";
        }
        else
        {
            // Move: copy then delete originals
            var result = await _sendToService.SendToAsync(paths, target);
            if (result.Success)
            {
                foreach (var p in paths)
                {
                    try
                    {
                        if (Directory.Exists(p)) Directory.Delete(p, true);
                        else if (File.Exists(p)) File.Delete(p);
                    }
                    catch { /* Best effort delete */ }
                }
                StatusText = $"Moved {paths.Count} item(s) to {Path.GetFileName(destination)}";
                await RefreshCurrentDirectoryAsync();
            }
            else
            {
                StatusText = result.Error ?? "Move failed";
            }
        }
    }

    [RelayCommand]
    private void CancelMoveTo()
    {
        IsMoveToDialogVisible = false;
    }

    private void LoadMoveToItems(string path)
    {
        MoveToItems.Clear();
        try
        {
            var dirInfo = new DirectoryInfo(path);
            foreach (var dir in dirInfo.EnumerateDirectories().OrderBy(d => d.Name))
            {
                if ((dir.Attributes & FileAttributes.Hidden) != 0) continue;
                if ((dir.Attributes & FileAttributes.System) != 0) continue;
                MoveToItems.Add(new FileSystemItem
                {
                    Name = dir.Name,
                    Path = dir.FullName,
                    Type = FileSystemItemType.Directory,
                    LastModified = dir.LastWriteTime
                });
            }
        }
        catch
        {
            // Access denied or other error — show empty
        }
    }

    [RelayCommand]
    private void CutSelected()
    {
        var paths = GetSelectedPaths();
        if (paths.Count > 0)
            _clipboardService.SetCut(paths);
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        if (!_clipboardService.HasContent || _clipboardService.Current is null) return;

        using var _suppress = _fileWatcherService.Suppress();
        var op = _clipboardService.Current;
        _applyToAllAction = null;
        ApplyConflictToAll = false;

        var sourcePaths = op.Paths.ToList();
        var title = op.IsCut
            ? (sourcePaths.Count == 1 ? $"Moving '{Path.GetFileName(sourcePaths[0])}'" : $"Moving {sourcePaths.Count} items")
            : (sourcePaths.Count == 1 ? $"Copying '{Path.GetFileName(sourcePaths[0])}'" : $"Copying {sourcePaths.Count} items");

        FileOperationResult result;
        if (op.IsCut)
        {
            result = await _fileOperationManager.MoveAsync(sourcePaths, CurrentPath, title,
                conflictResolver: HandleConflictAsync);
        }
        else
        {
            result = await _fileOperationManager.CopyAsync(sourcePaths, CurrentPath, title,
                conflictResolver: HandleConflictAsync);
        }

        if (result.Success) _clipboardService.Clear();

        // Record in undo history
        if (result.Success && result.ItemsProcessed > 0)
        {
            var opType = op.IsCut ? UndoOperationType.Move : UndoOperationType.Copy;
            var entries = op.Paths.Select(p => new UndoEntry
            {
                SourcePath = p,
                DestinationPath = Path.Combine(CurrentPath, Path.GetFileName(p)),
                IsDirectory = Directory.Exists(Path.Combine(CurrentPath, Path.GetFileName(p)))
            }).ToList();

            var desc = entries.Count == 1
                ? $"{(op.IsCut ? "Move" : "Copy")} '{Path.GetFileName(entries[0].SourcePath)}'"
                : $"{(op.IsCut ? "Move" : "Copy")} {entries.Count} items";

            _historyService.AddOperation(new UndoableOperation
            {
                Type = opType,
                Description = desc,
                Entries = entries
            });
        }

        await RefreshCurrentDirectoryAsync();
        if (!result.Success && !result.Cancelled && result.Error is not null)
            StatusText = result.Error;
    }

    // --- Drag & Drop ---

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private string? _dropTargetPath;

    /// <summary>
    /// Executes a drop operation: moves (internal) or copies (external) source files to the destination.
    /// Reuses the same file operation infrastructure as Paste.
    /// </summary>
    public async Task ExecuteDropAsync(IReadOnlyList<string> sourcePaths, string destinationPath, DropEffect effect)
    {
        if (sourcePaths.Count == 0 || string.IsNullOrEmpty(destinationPath))
            return;

        using var _suppress = _fileWatcherService.Suppress();

        _applyToAllAction = null;
        ApplyConflictToAll = false;

        var isCopy = effect == DropEffect.Copy;
        var title = isCopy
            ? (sourcePaths.Count == 1 ? $"Copying '{Path.GetFileName(sourcePaths[0])}'" : $"Copying {sourcePaths.Count} items")
            : (sourcePaths.Count == 1 ? $"Moving '{Path.GetFileName(sourcePaths[0])}'" : $"Moving {sourcePaths.Count} items");

        FileOperationResult result;
        if (isCopy)
        {
            result = await _fileOperationManager.CopyAsync(sourcePaths, destinationPath, title,
                conflictResolver: HandleConflictAsync);
        }
        else
        {
            result = await _fileOperationManager.MoveAsync(sourcePaths, destinationPath, title,
                conflictResolver: HandleConflictAsync);
        }

        // Record undo
        if (result.Success && result.ItemsProcessed > 0)
        {
            var opType = isCopy ? UndoOperationType.Copy : UndoOperationType.Move;
            var entries = sourcePaths.Select(p => new UndoEntry
            {
                SourcePath = p,
                DestinationPath = Path.Combine(destinationPath, Path.GetFileName(p)),
                IsDirectory = Directory.Exists(Path.Combine(destinationPath, Path.GetFileName(p)))
            }).ToList();

            var desc = entries.Count == 1
                ? $"{(isCopy ? "Copy" : "Move")} '{Path.GetFileName(entries[0].SourcePath)}'"
                : $"{(isCopy ? "Copy" : "Move")} {entries.Count} items";

            _historyService.AddOperation(new UndoableOperation
            {
                Type = opType,
                Description = desc,
                Entries = entries
            });
        }

        IsDragOver = false;
        DropTargetPath = null;

        if (!result.Success && !result.Cancelled && result.Error is not null)
            StatusText = result.Error;
        
        // Refresh if the current path is the destination or the source
        LoadDirectory(CurrentPath);
    }

    /// <summary>
    /// Gets paths of currently selected items for starting a drag operation.
    /// If the specified item is not in the selection, selects it first.
    /// </summary>
    public IReadOnlyList<string> GetDragPaths(FileSystemItem? draggedItem)
    {
        if (draggedItem is null || draggedItem.IsGroupHeader)
            return [];

        // If the dragged item is already in the selection, drag the whole selection
        if (SelectedItems.Contains(draggedItem))
            return SelectedItems.Select(i => i.Path).ToList();

        // Otherwise, select just this item
        SelectedItem = draggedItem;
        return [draggedItem.Path];
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;

        // Confirmation is handled by the View showing a dialog
        IsDeleteConfirmationVisible = true;
        DeleteConfirmationMessage = paths.Count == 1
            ? $"Move '{Path.GetFileName(paths[0])}' to the Recycle Bin?"
            : $"Move {paths.Count} items to the Recycle Bin?";
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        IsDeleteConfirmationVisible = false;
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;

        using var _suppress = _fileWatcherService.Suppress();

        var useRecycleBin = !_isPermanentDelete;
        var title = useRecycleBin
            ? (paths.Count == 1 ? $"Moving '{Path.GetFileName(paths[0])}' to Recycle Bin" : $"Moving {paths.Count} items to Recycle Bin")
            : (paths.Count == 1 ? $"Deleting '{Path.GetFileName(paths[0])}'" : $"Deleting {paths.Count} items");

        var result = await _fileOperationManager.DeleteAsync(paths, useRecycleBin, title);

        _isPermanentDelete = false;
        if (result.Success)
        {
            // Record in undo history (delete to recycle bin is not safely reversible programmatically)
            var entries = paths.Select(p => new UndoEntry
            {
                SourcePath = p,
                DestinationPath = "",
                IsDirectory = Directory.Exists(p) // Note: may be false since item is already deleted
            }).ToList();

            var desc = entries.Count == 1
                ? $"Delete '{Path.GetFileName(entries[0].SourcePath)}'"
                : $"Delete {entries.Count} items";

            _historyService.AddOperation(new UndoableOperation
            {
                Type = UndoOperationType.Delete,
                Description = desc,
                Entries = entries,
                IsReversible = false // Cannot programmatically restore from recycle bin
            });

            await RefreshCurrentDirectoryAsync();
        }
        else if (result.Cancelled)
            StatusText = "Delete cancelled";
        else if (result.Error is not null)
            StatusText = result.Error;
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsDeleteConfirmationVisible = false;
        _isPermanentDelete = false;
    }

    [RelayCommand]
    private async Task PermanentDeleteSelectedAsync()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;

        IsDeleteConfirmationVisible = true;
        DeleteConfirmationMessage = paths.Count == 1
            ? $"Permanently delete '{Path.GetFileName(paths[0])}'? This cannot be undone."
            : $"Permanently delete {paths.Count} items? This cannot be undone.";
        _isPermanentDelete = true;
    }

    [RelayCommand]
    private void StartRename()
    {
        if (SelectedItem is null) return;
        RenameText = SelectedItem.Name;
        IsRenaming = true;
    }

    [RelayCommand]
    private async Task ConfirmRenameAsync()
    {
        if (!IsRenaming || SelectedItem is null) return;
        IsRenaming = false;

        var newName = RenameText.Trim();
        if (string.IsNullOrEmpty(newName) || newName == SelectedItem.Name) return;

        var oldPath = SelectedItem.Path;
        var directory = Path.GetDirectoryName(oldPath) ?? "";
        var newPath = Path.Combine(directory, newName);
        var isDir = SelectedItem.Type == Core.Models.FileSystemItemType.Directory;

        var result = await _fileOpService.RenameAsync(oldPath, newName);
        if (result.Success)
        {
            _historyService.AddOperation(new UndoableOperation
            {
                Type = UndoOperationType.Rename,
                Description = $"Rename '{Path.GetFileName(oldPath)}'",
                Entries = [new UndoEntry { SourcePath = oldPath, DestinationPath = newPath, IsDirectory = isDir }]
            });
            await RefreshCurrentDirectoryAsync();
        }
        else if (result.Error is not null)
            StatusText = result.Error;
    }

    [RelayCommand]
    private void CancelRename()
    {
        IsRenaming = false;
    }

    [RelayCommand]
    private void ResolveConflict(string actionString)
    {
        if (Enum.TryParse<ConflictAction>(actionString, out var action))
        {
            if (ApplyConflictToAll)
            {
                _applyToAllAction = action;
            }

            _conflictTcs?.TrySetResult(action);
            IsConflictResolutionVisible = false;
        }
    }

    private Task<ConflictAction> HandleConflictAsync(FileConflict conflict)
    {
        // If "Apply to All" was previously selected, use the remembered action
        if (_applyToAllAction.HasValue)
        {
            return Task.FromResult(_applyToAllAction.Value);
        }

        _conflictTcs = new TaskCompletionSource<ConflictAction>();
        
        ConflictMessage = $"The destination already has a {(conflict.IsDirectory ? "folder" : "file")} named '{conflict.FileName}'.";
        
        if (conflict.IsDirectory)
        {
            ConflictSourceInfo = "Folder";
            ConflictDestinationInfo = "Folder";
        }
        else
        {
            ConflictSourceInfo = $"{FormatSize(conflict.SourceSize)}\n{conflict.SourceModified:g}";
            ConflictDestinationInfo = $"{FormatSize(conflict.DestinationSize)}\n{conflict.DestinationModified:g}";
        }

        IsConflictResolutionVisible = true;
        return _conflictTcs.Task;
    }

    private static string FormatSize(long? bytes)
    {
        if (bytes is null) return "Unknown size";
        string[] suf = { "B", "KB", "MB", "GB", "TB" };
        if (bytes.Value == 0) return "0 B";
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytes.Value, 1024)));
        var num = Math.Round(bytes.Value / Math.Pow(1024, place), 1);
        return $"{num} {suf[place]}";
    }

    [RelayCommand]
    private void CreateFolder()
    {
        _isCreateDialogForFolder = true;
        CreateDialogTitle = "New Folder";
        CreateDialogName = "New Folder";
        CreateDialogError = string.Empty;
        IsCreateDialogVisible = true;
    }

    [RelayCommand]
    private void CreateFile()
    {
        _isCreateDialogForFolder = false;
        CreateDialogTitle = "New File";
        CreateDialogName = "New Text Document.txt";
        CreateDialogError = string.Empty;
        IsCreateDialogVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmCreateAsync()
    {
        var name = CreateDialogName?.Trim();

        // Validation
        if (string.IsNullOrWhiteSpace(name))
        {
            CreateDialogError = "Name cannot be empty.";
            return;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (name.IndexOfAny(invalidChars) >= 0)
        {
            CreateDialogError = "Name contains invalid characters.";
            return;
        }

        var targetPath = Path.Combine(CurrentPath, name);
        if (Directory.Exists(targetPath) || File.Exists(targetPath))
        {
            CreateDialogError = _isCreateDialogForFolder
                ? "A folder with this name already exists."
                : "A file with this name already exists.";
            return;
        }

        IsCreateDialogVisible = false;

        if (_isCreateDialogForFolder)
        {
            var (result, createdPath) = await _fileOpService.CreateDirectoryAsync(CurrentPath, name);
            if (result.Success && createdPath is not null)
            {
                _historyService.AddOperation(new UndoableOperation
                {
                    Type = UndoOperationType.CreateFolder,
                    Description = $"Create folder '{name}'",
                    Entries = [new UndoEntry { SourcePath = createdPath, DestinationPath = createdPath, IsDirectory = true }]
                });
                await RefreshCurrentDirectoryAsync();
                var newItem = Items.FirstOrDefault(i => i.Name == name);
                if (newItem is not null) SelectedItem = newItem;
            }
            else if (result.Error is not null)
            {
                StatusText = result.Error;
            }
        }
        else
        {
            var (result, createdPath) = await _fileOpService.CreateFileAsync(CurrentPath, name);
            if (result.Success && createdPath is not null)
            {
                _historyService.AddOperation(new UndoableOperation
                {
                    Type = UndoOperationType.CreateFile,
                    Description = $"Create '{name}'",
                    Entries = [new UndoEntry { SourcePath = createdPath, DestinationPath = createdPath, IsDirectory = false }]
                });
                await RefreshCurrentDirectoryAsync();
                var newItem = Items.FirstOrDefault(i => i.Name == name);
                if (newItem is not null) SelectedItem = newItem;
            }
            else if (result.Error is not null)
            {
                StatusText = result.Error;
            }
        }
    }

    [RelayCommand]
    private void CancelCreate()
    {
        IsCreateDialogVisible = false;
        CreateDialogError = string.Empty;
    }

    // --- File Operation Center commands ---

    [RelayCommand]
    private void MinimizeFileOperationCenter() => IsFileOperationCenterVisible = false;

    [RelayCommand]
    private void ShowFileOperationCenter() => IsFileOperationCenterVisible = true;

    public void RequestCloseConfirmation()
    {
        CloseConfirmationMessage = "A file operation is currently running.\nClosing Nexus now may interrupt it.";
        IsCloseConfirmationVisible = true;
    }

    [RelayCommand]
    private void ContinueUsing() => IsCloseConfirmationVisible = false;

    /// <summary>
    /// Keeps the app open until the running operations finish, then exits automatically.
    /// The File Operation Center stays visible so the user can watch progress.
    /// </summary>
    [RelayCommand]
    private async Task WaitForOperation()
    {
        IsCloseConfirmationVisible = false;
        IsFileOperationCenterVisible = true;

        await _fileOperationManager.WaitForAllAsync();
        ApplicationExitRequested?.Invoke();
    }

    [RelayCommand]
    private async Task CancelAndExitAsync()
    {
        IsCloseConfirmationVisible = false;
        await _fileOperationManager.CancelAllAndWaitAsync();
        ApplicationExitRequested?.Invoke();
    }

    // --- Undo ---

    public bool CanUndo => _historyService?.CanUndo ?? false;
    public string? UndoDescription => _historyService?.UndoDescription;
    public bool CanRedo => _historyService?.CanRedo ?? false;
    public string? RedoDescription => _historyService?.RedoDescription;

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (_historyService is null || !_historyService.CanUndo) return;

        var result = await _historyService.UndoAsync();
        if (result.Success)
        {
            await RefreshCurrentDirectoryAsync();
            StatusText = $"Undone ({result.ItemsReverted} item{(result.ItemsReverted != 1 ? "s" : "")})";
        }
        else if (result.Error is not null)
        {
            StatusText = result.Error;
        }
    }

    [RelayCommand]
    private async Task RedoAsync()
    {
        if (_historyService is null || !_historyService.CanRedo) return;

        var result = await _historyService.RedoAsync();
        if (result.Success)
        {
            await RefreshCurrentDirectoryAsync();
            StatusText = $"Redone ({result.ItemsReverted} item{(result.ItemsReverted != 1 ? "s" : "")})";
        }
        else if (result.Error is not null)
        {
            StatusText = result.Error;
        }
    }

    [RelayCommand]
    private async Task DuplicateSelectedAsync()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;

        foreach (var path in paths)
        {
            try
            {
                var dir = Path.GetDirectoryName(path) ?? CurrentPath;
                var name = Path.GetFileNameWithoutExtension(path);
                var ext = Path.GetExtension(path);
                var newPath = Path.Combine(dir, $"{name} - Copy{ext}");
                var counter = 1;
                while (File.Exists(newPath) || Directory.Exists(newPath))
                {
                    newPath = Path.Combine(dir, $"{name} - Copy ({counter}){ext}");
                    counter++;
                }

                if (Directory.Exists(path))
                    await Task.Run(() => CopyDirectoryRecursive(path, newPath));
                else if (File.Exists(path))
                    File.Copy(path, newPath);
            }
            catch (Exception ex)
            {
                StatusText = $"Duplicate failed: {ex.Message}";
                return;
            }
        }

        await RefreshCurrentDirectoryAsync();
        StatusText = $"Duplicated {paths.Count} item{(paths.Count != 1 ? "s" : "")}";
    }

    private static void CopyDirectoryRecursive(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectoryRecursive(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    [RelayCommand]
    private void SetExtraLargeIconsView() => SetViewMode(ExplorerViewMode.ExtraLargeIcons);

    [RelayCommand]
    private void SetLargeIconsView() => SetViewMode(ExplorerViewMode.LargeIcons);

    [RelayCommand]
    private void SetMediumIconsView() => SetViewMode(ExplorerViewMode.MediumIcons);

    [RelayCommand]
    private void SetSmallIconsView() => SetViewMode(ExplorerViewMode.SmallIcons);

    [RelayCommand]
    private void SetListView() => SetViewMode(ExplorerViewMode.List);

    [RelayCommand]
    private void SetDetailsView() => SetViewMode(ExplorerViewMode.Details);

    private void SetViewMode(ExplorerViewMode mode)
    {
        ViewMode = mode;
        if (_tabService?.ActiveTab is not null)
            _tabService.ActiveTab.ViewMode = mode;
    }

    [RelayCommand]
    private async Task OpenInTerminalAsync(FileSystemItem? item)
    {
        if (item is null) return;

        // Determine the target directory
        var targetPath = Helpers.TerminalPathHelper.GetTerminalWorkingDirectory(item, CurrentPath);
        if (targetPath is null) return;

        await OpenTerminalAtDirectoryAsync(targetPath);
    }

    [RelayCommand]
    private async Task OpenCurrentInTerminalAsync()
    {
        var targetPath = Helpers.TerminalPathHelper.GetTerminalWorkingDirectory(null, CurrentPath);
        if (targetPath is null) return;

        await OpenTerminalAtDirectoryAsync(targetPath);
    }

    [RelayCommand]
    private void OpenInOtherTerminal(TerminalProfile? profile)
    {
        if (profile is null) return;

        var targetPath = Helpers.TerminalPathHelper.GetTerminalWorkingDirectory(SelectedItem, CurrentPath);
        OpenExternalTerminal(profile, targetPath);
    }

    [RelayCommand]
    private void OpenCurrentInOtherTerminal(TerminalProfile? profile)
    {
        if (profile is null) return;

        var targetPath = Helpers.TerminalPathHelper.GetTerminalWorkingDirectory(null, CurrentPath);
        OpenExternalTerminal(profile, targetPath);
    }

    private void OpenExternalTerminal(TerminalProfile profile, string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
        {
            StatusText = "The target folder no longer exists.";
            _logger.LogWarning("Cannot open terminal '{Name}': directory does not exist ({Directory}).",
                profile.Name, targetPath);
            return;
        }

        try
        {
            _logger.LogInformation("Launching {Name} in {Directory}.", profile.Name, targetPath);
            _terminalLauncher.Launch(profile, targetPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch terminal {Name} in {Directory}.", profile.Name, targetPath);
            StatusText = $"Cannot open {profile.Name}: {ex.Message}";
        }
    }

    private async Task OpenTerminalAtDirectoryAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // Ensure terminal panel is visible (Split mode)
        if (LayoutMode != LayoutMode.Split)
        {
            LayoutMode = LayoutMode.Split;
            IsExplorerVisible = true;
            IsTerminalVisible = true;

            if (_tabService?.ActiveTab is not null)
            {
                _tabService.ActiveTab.LayoutMode = LayoutMode.Split;
                _tabService.ActiveTab.IsTerminalOpen = true;
            }
        }

        // Open terminal at the specified path
        await Terminal.OpenTerminalAtPathAsync(path);
    }

    /// <summary>
    /// Raised when the Properties dialog should be shown for the given items.
    /// The App layer handles this by creating and showing the PropertiesWindow.
    /// </summary>
    public event Action<IReadOnlyList<FileSystemItem>>? ShowPropertiesRequested;

    [RelayCommand]
    private Task ShowPropertiesAsync(FileSystemItem? item)
    {
        // Determine which items to show. If the invoked item is part of the current
        // multi-selection (or no specific item was passed), show the whole selection;
        // otherwise show just the invoked item. This matches Windows Explorer behavior:
        // selecting several items and choosing Properties opens ONE combined dialog.
        IReadOnlyList<FileSystemItem> items;
        if (item is not null && !SelectedItems.Contains(item))
        {
            items = [item];
        }
        else if (SelectedItems.Count > 0)
        {
            items = SelectedItems.ToList();
        }
        else if (item is not null)
        {
            items = [item];
        }
        else if (SelectedItem is not null)
        {
            items = [SelectedItem];
        }
        else
        {
            return Task.CompletedTask;
        }

        ShowPropertiesRequested?.Invoke(items);
        return Task.CompletedTask;
    }

    private IReadOnlyList<string> GetSelectedPaths()
    {
        if (SelectedItems.Count > 0)
            return SelectedItems.Select(i => i.Path).ToList();
        if (SelectedItem is not null)
            return [SelectedItem.Path];
        return [];
    }

    private async Task RefreshCurrentDirectoryAsync()
    {
        var path = _tabService.ActiveTab.Refresh();
        LoadDirectory(path);
        await Task.CompletedTask;
    }

    private void OnFileOperationsChanged(object? sender, EventArgs e)
    {
        var active = HasActiveOperations;
        if (active && !_fileOperationsWereActive)
            IsFileOperationCenterVisible = true;
        else if (!active && _fileOperationsWereActive)
            IsFileOperationCenterVisible = false;

        _fileOperationsWereActive = active;

        OnPropertyChanged(nameof(HasActiveOperations));
        OnPropertyChanged(nameof(OperationSummaryText));
        OnPropertyChanged(nameof(OverallProgress));
        OnPropertyChanged(nameof(IsOperationIndicatorVisible));
    }

    private void OnFileWatcherChanged(object? sender, EventArgs e)
    {
        // Marshal to UI thread and refresh if we're not already loading
        if (IsLoading || IsSearchActive || IsOperationInProgress || HasActiveOperations) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!IsLoading && !IsSearchActive && !IsOperationInProgress && !HasActiveOperations)
            {
                var path = _tabService?.ActiveTab?.CurrentPath;
                // Silent refresh: diff-merge so the list doesn't flicker or lose selection.
                if (!string.IsNullOrEmpty(path) && !VirtualPaths.IsVirtual(path))
                    LoadDirectory(path, silent: true);
            }
        });
    }

    [RelayCommand]
    private async Task OpenItemAsync(FileSystemItem? item)
    {
        if (item is null) return;

        if (item.Type is FileSystemItemType.Directory or FileSystemItemType.Drive)
        {
            _tabService.ActiveTab.NavigateTo(item.Path);
            LoadDirectory(item.Path);
        }
        else
        {
            try
            {
                await _platformService.OpenWithDefaultAsync(item.Path);
            }
            catch
            {
                // No default app associated — open "Open With" dialog instead
                try
                {
                    await _platformService.OpenWithDialogAsync(item.Path);
                }
                catch (Exception ex2)
                {
                    StatusText = $"Cannot open file: {ex2.Message}";
                }
            }
        }
    }

    [RelayCommand]
    private async Task OpenWithAsync(FileSystemItem? item)
    {
        if (item is null || item.Type == FileSystemItemType.Directory) return;

        try
        {
            await _platformService.OpenWithDialogAsync(item.Path);
        }
        catch (Exception ex)
        {
            StatusText = $"Cannot open: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ShowInExplorerAsync(FileSystemItem? item)
    {
        if (item is null) return;

        try
        {
            await _platformService.ShowInSystemExplorerAsync(item.Path);
        }
        catch (Exception ex)
        {
            StatusText = $"Cannot show in explorer: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenInNewTab(FileSystemItem? item)
    {
        if (item is null || item.Type != FileSystemItemType.Directory) return;
        _tabService.CreateTab(item.Path);
    }

    [RelayCommand]
    private void SidebarNavigate(NavigationItem? item)
    {
        if (item is null) return;
        _tabService.ActiveTab.NavigateTo(item.Path);
        LoadDirectory(item.Path);
    }

    [RelayCommand]
    private void PinToFavorites(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        if (_pinnedFavorites.Contains(path, StringComparer.OrdinalIgnoreCase)) return;

        _pinnedFavorites.Add(path);

        var favorites = FindGroup(SidebarGroupIds.Favorites);
        favorites?.Items.Add(new NavigationItem
        {
            Name = Path.GetFileName(path) ?? path,
            Path = path,
            Kind = NavigationItemKind.Favorite,
            Section = NavigationSection.Favorites
        });

        StatusText = $"Pinned '{Path.GetFileName(path)}' to Quick Access";
    }

    [RelayCommand]
    private void UnpinFromFavorites(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        _pinnedFavorites.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

        var favorites = FindGroup(SidebarGroupIds.Favorites);
        var item = favorites?.Items.FirstOrDefault(n =>
            n.Kind == NavigationItemKind.Favorite &&
            string.Equals(n.Path, path, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
            favorites!.Items.Remove(item);

        StatusText = $"Unpinned '{Path.GetFileName(path)}' from Quick Access";
    }

    /// <summary>
    /// Whether the given path is pinned in favorites.
    /// </summary>
    public bool IsPinned(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return _pinnedFavorites.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void PinCurrentFolder()
    {
        PinToFavorites(CurrentPath);
    }

    [RelayCommand]
    private void BreadcrumbNavigate(BreadcrumbItem? item)
    {
        if (item is null) return;
        _tabService.ActiveTab.NavigateTo(item.Path);
        LoadDirectory(item.Path);
    }

    [RelayCommand]
    private void StartAddressBarEdit()
    {
        AddressBarText = CurrentPath;
        IsAddressBarEditing = true;
    }

    [RelayCommand]
    private void CancelAddressBarEdit()
    {
        IsAddressBarEditing = false;
        AddressBarText = CurrentPath;
    }

    /// <summary>
    /// Gets autocomplete suggestions for the current address bar text.
    /// Returns the first matching subdirectory path, or null if no match.
    /// </summary>
    public string? GetAutocompleteSuggestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            // Get the directory portion and the partial name
            var dir = Path.GetDirectoryName(text);
            var partial = Path.GetFileName(text);

            if (string.IsNullOrEmpty(dir)) return null;
            if (string.IsNullOrEmpty(partial)) return null;
            if (!Directory.Exists(dir)) return null;

            var match = Directory.EnumerateDirectories(dir)
                .Select(d => Path.GetFileName(d))
                .Where(name => name != null && name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (match is not null)
                return Path.Combine(dir, match);
        }
        catch
        {
            // Ignore filesystem errors during autocomplete
        }

        return null;
    }

    /// <summary>
    /// Gets the immediate subdirectories of a given path for breadcrumb dropdown.
    /// </summary>
    public IReadOnlyList<string> GetSubfolders(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return [];
            return Directory.EnumerateDirectories(path)
                .Select(d => Path.GetFileName(d) ?? d)
                .Where(name => !name.StartsWith('$') && !name.StartsWith('.'))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Take(30) // Limit for performance
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Copies the current path to the system clipboard via event.
    /// </summary>
    [RelayCommand]
    private void CopyCurrentPath()
    {
        var path = VirtualPaths.IsVirtual(CurrentPath) ? VirtualPaths.GetDisplayName(CurrentPath) : CurrentPath;
        CopyTextToClipboardRequested?.Invoke(path);
        StatusText = "Path copied";
    }

    // --- Private methods ---

    private void OnActiveTabChanged(object? sender, TabItem tab)
    {
        LoadDirectory(tab.CurrentPath);
        SelectedItem = tab.SelectedItem;
        // Note: do NOT rebuild the Tabs collection here. Activation only changes which tab is
        // active (handled by the IsActive binding); rebuilding the collection on every navigation
        // caused the tab strip to visually reshuffle.

        // Restore layout state
        LayoutMode = tab.LayoutMode;
        SplitOrientation = tab.SplitOrientation;
        SplitRatio = tab.SplitRatio;   // uses clamped setter
        ViewMode = tab.ViewMode;
        
        // Search, Sort, Group state
        SortMode = tab.SortMode;
        SortDirection = tab.SortDirection;
        GroupMode = tab.GroupMode;
        ShowHiddenFiles = tab.ShowHiddenFiles;
        SearchQuery = tab.SearchQuery;
        IsSearchActive = tab.SearchActive;

        IsExplorerVisible = tab.LayoutMode != LayoutMode.TerminalOnly;
        IsTerminalVisible = tab.LayoutMode != LayoutMode.ExplorerOnly;

        // Switch terminal to this tab's state
        Terminal.SwitchToTab(tab);

        // Auto-restart terminal session if the tab had the terminal open
        // but the session is gone (e.g. after app restart)
        if (IsTerminalVisible && !Terminal.IsSessionActive)
        {
            _ = Terminal.OpenTerminalAsync();
        }
    }

    private void OnTabsChanged(object? sender, EventArgs e)
    {
        SyncTabCollection();
    }

    private void OnTerminalDirectoryChanged(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!Directory.Exists(path)) return;
        if (string.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase)) return;

        _tabService.ActiveTab.NavigateTo(path);
        LoadDirectory(path);
    }

    private void OnTerminalCloseRequested()
    {
        _ = SetLayoutModeAsync(LayoutMode.ExplorerOnly);
    }

    private void SyncTabCollection()
    {
        var source = _tabService.Tabs;

        // Reconcile in place instead of Clear()+re-add so the tab strip never flickers or
        // visually reshuffles. Remove tabs no longer present, then add/move to match order.
        for (var i = Tabs.Count - 1; i >= 0; i--)
        {
            if (!source.Contains(Tabs[i]))
                Tabs.RemoveAt(i);
        }

        for (var i = 0; i < source.Count; i++)
        {
            var tab = source[i];
            if (i >= Tabs.Count)
            {
                Tabs.Add(tab);
            }
            else if (!ReferenceEquals(Tabs[i], tab))
            {
                var existing = Tabs.IndexOf(tab);
                if (existing >= 0)
                    Tabs.Move(existing, i);
                else
                    Tabs.Insert(i, tab);
            }
        }
    }

    private void LoadDirectory(string path) => LoadDirectory(path, silent: false);

    /// <summary>
    /// Loads a directory into the file list.
    /// When <paramref name="silent"/> is true (e.g. an automatic refresh triggered by the file
    /// watcher), the existing <see cref="Items"/> collection is diff-merged instead of cleared
    /// and repopulated, so the list doesn't flicker/reset selection when nothing visible changed.
    /// </summary>
    private async void LoadDirectory(string path, bool silent)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        if (!silent)
        {
            CurrentPath = path;
            AddressBarText = VirtualPaths.IsVirtual(path) ? VirtualPaths.GetDisplayName(path) : path;
            IsAddressBarEditing = false;
            HasError = false;
            ErrorMessage = string.Empty;
            IsLoading = true;
            IsEmpty = false;
            SelectedItem = null;

            _logger.LogDebug("Loading directory: {Path}", path);

            Items.Clear();
            UpdateBreadcrumbs(path);
            NotifyNavigationState();
        }

        // Update the tab's folder color from the color service
        if (_tabService?.ActiveTab is { } activeTab && _folderColorService is not null)
            activeTab.FolderColor = _folderColorService.GetColor(path);

        try
        {
            IEnumerable<FileSystemItem> resultItems;

            if (path == VirtualPaths.ThisPC)
            {
                // Load drives as FileSystemItems for This PC view
                resultItems = await LoadThisPCItemsAsync(ct);
            }
            else if (path == VirtualPaths.Network)
            {
                // Load network drives
                resultItems = await LoadNetworkItemsAsync(ct);
            }
            else if (VirtualPaths.IsColorGroup(path))
            {
                // Virtual view: all folders assigned a specific color, gathered from any location.
                resultItems = await LoadColorGroupItemsAsync(path, ct);
            }
            else
            {
                var items = await _fileSystemService.GetItemsAsync(path, ct);
                ct.ThrowIfCancellationRequested();
                resultItems = items;
            }

            // Filter hidden files if the option is disabled
            var filteredItems = ShowHiddenFiles
                ? resultItems.ToList()
                : resultItems.Where(i => !i.IsHidden).ToList();

            // Stamp the custom folder color onto directory items so the UI can show a color
            // swatch and sort/group by color without querying the service per item.
            filteredItems = StampFolderColors(filteredItems);

            var sortedItems = SortItems(filteredItems).ToList();

            if (silent)
            {
                // Diff-merge into the existing collection so unchanged rows are left untouched
                // (no flicker, selection preserved). Only add/remove what actually changed.
                MergeItems(sortedItems);
            }
            else
            {
                foreach (var item in sortedItems)
                    Items.Add(item);
            }

            ItemCount = Items.Count;
            IsEmpty = ItemCount == 0;
            StatusText = ItemCount == 0 ? "This folder is empty" : $"{ItemCount} items";

            // Start watching this directory for external changes
            if (!VirtualPaths.IsVirtual(path))
                _fileWatcherService.Watch(path);
            else
                _fileWatcherService.StopWatching();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied loading directory: {Path}", path);
            // On a silent background refresh, don't disrupt the view with an error overlay.
            if (silent) return;
            HasError = true;
            ErrorMessage = "Access denied. You don't have permission to view this folder.";
            StatusText = "Access denied";
            ItemCount = 0;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning(ex, "Directory not found: {Path}", path);
            if (silent) return;
            HasError = true;
            ErrorMessage = $"The folder \"{Path.GetFileName(path)}\" no longer exists.";
            StatusText = "Folder not found";
            ItemCount = 0;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Cannot read directory: {Path}", path);
            if (silent) return;
            HasError = true;
            ErrorMessage = $"Cannot read folder: {ex.Message}";
            StatusText = "Error";
            ItemCount = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading directory: {Path}", path);
            if (silent) return;
            HasError = true;
            ErrorMessage = $"An unexpected error occurred: {ex.Message}";
            StatusText = "Error";
            ItemCount = 0;
        }
        finally
        {
            if (!silent)
                IsLoading = false;
        }
    }

    /// <summary>
    /// Reconciles the current <see cref="Items"/> collection with a freshly computed list,
    /// applying the minimal set of edits so unchanged rows stay in place (no flicker, no
    /// selection loss). A row is considered "the same" when its visible signature matches.
    /// </summary>
    private void MergeItems(List<FileSystemItem> newItems)
    {
        // Fast path: identical length and every signature matches → nothing to do.
        if (Items.Count == newItems.Count)
        {
            var identical = true;
            for (var i = 0; i < newItems.Count; i++)
            {
                if (!SameRow(Items[i], newItems[i]))
                {
                    identical = false;
                    break;
                }
            }
            if (identical) return;
        }

        // Remove rows (from the end) that are no longer present anywhere in the new list.
        var newKeys = new HashSet<string>(newItems.Select(RowKey), StringComparer.OrdinalIgnoreCase);
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (!newKeys.Contains(RowKey(Items[i])))
                Items.RemoveAt(i);
        }

        // Walk the target list and align Items to it, inserting/replacing as needed.
        for (var i = 0; i < newItems.Count; i++)
        {
            var target = newItems[i];

            if (i >= Items.Count)
            {
                Items.Add(target);
                continue;
            }

            if (SameRow(Items[i], target))
                continue;

            // If the target already exists later in the collection, move it up; otherwise insert.
            var existingIndex = -1;
            for (var j = i + 1; j < Items.Count; j++)
            {
                if (string.Equals(RowKey(Items[j]), RowKey(target), StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                // Content may have changed (size/date/color) — replace so the row updates.
                if (SameRow(Items[existingIndex], target))
                    Items.Move(existingIndex, i);
                else
                {
                    Items.RemoveAt(existingIndex);
                    Items.Insert(i, target);
                }
            }
            else
            {
                Items.Insert(i, target);
            }
        }

        // Trim any trailing leftovers.
        while (Items.Count > newItems.Count)
            Items.RemoveAt(Items.Count - 1);
    }

    /// <summary>Stable identity of a row (path, or the group name for headers).</summary>
    private static string RowKey(FileSystemItem item)
        => item.IsGroupHeader ? "\u0000header:" + item.GroupName : item.Path;

    /// <summary>True when two rows are visually identical (same identity and displayed data).</summary>
    private static bool SameRow(FileSystemItem a, FileSystemItem b)
    {
        return a.IsGroupHeader == b.IsGroupHeader
            && string.Equals(RowKey(a), RowKey(b), StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Name, b.Name, StringComparison.Ordinal)
            && a.Size == b.Size
            && a.LastModified == b.LastModified
            && string.Equals(a.FolderColor, b.FolderColor, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.GroupName, b.GroupName, StringComparison.Ordinal);
    }

    private async Task<IEnumerable<FileSystemItem>> LoadThisPCItemsAsync(CancellationToken ct)
    {
        var driveItems = await _fileSystemService.GetDriveItemsAsync(ct);
        ct.ThrowIfCancellationRequested();

        var result = new List<FileSystemItem>();

        // Group: Devices and Drives
        var localDrives = driveItems.Where(d => d.Category is DriveCategory.Fixed or DriveCategory.Removable or DriveCategory.Optical or DriveCategory.Ram).ToList();
        if (localDrives.Count > 0)
        {
            result.Add(new FileSystemItem
            {
                IsGroupHeader = true,
                GroupName = "Devices and Drives",
                Name = "Devices and Drives",
                Path = "",
                Type = FileSystemItemType.File
            });
            foreach (var d in localDrives)
            {
                result.Add(new FileSystemItem
                {
                    Name = d.Name,
                    Path = d.Path,
                    Type = FileSystemItemType.Drive,
                    Size = d.IsReady ? d.TotalSize : null,
                    TotalSpace = d.IsReady ? d.TotalSize : null,
                    FreeSpace = d.IsReady ? d.FreeSpace : null
                });
            }
        }

        // Group: Network Locations
        var networkDrives = driveItems.Where(d => d.Category == DriveCategory.Network).ToList();
        if (networkDrives.Count > 0)
        {
            result.Add(new FileSystemItem
            {
                IsGroupHeader = true,
                GroupName = "Network Locations",
                Name = "Network Locations",
                Path = "",
                Type = FileSystemItemType.File
            });
            foreach (var d in networkDrives)
            {
                result.Add(new FileSystemItem
                {
                    Name = d.Name,
                    Path = d.Path,
                    Type = FileSystemItemType.Drive,
                    Size = d.IsReady ? d.TotalSize : null,
                    TotalSpace = d.IsReady ? d.TotalSize : null,
                    FreeSpace = d.IsReady ? d.FreeSpace : null
                });
            }
        }

        return result;
    }

    private async Task<IEnumerable<FileSystemItem>> LoadNetworkItemsAsync(CancellationToken ct)
    {
        var driveItems = await _fileSystemService.GetDriveItemsAsync(ct);
        ct.ThrowIfCancellationRequested();

        var networkDrives = driveItems.Where(d => d.Category == DriveCategory.Network).ToList();

        if (networkDrives.Count == 0)
        {
            // Show informational status for empty network view
            StatusText = "No network drives mapped. Use the address bar (Ctrl+L) to navigate to \\\\server\\share";
            return Enumerable.Empty<FileSystemItem>();
        }

        var result = new List<FileSystemItem>();
        foreach (var d in networkDrives)
        {
            result.Add(new FileSystemItem
            {
                Name = d.Name,
                Path = d.Path,
                Type = FileSystemItemType.Drive,
                Size = d.IsReady ? d.TotalSize : null,
                TotalSpace = d.IsReady ? d.TotalSize : null,
                FreeSpace = d.IsReady ? d.FreeSpace : null
            });
        }
        return result;
    }

    /// <summary>
    /// Copies each directory item with its assigned custom color stamped onto FolderColor.
    /// Non-directory items and group headers pass through unchanged.
    /// </summary>
    private List<FileSystemItem> StampFolderColors(List<FileSystemItem> items)
    {
        if (_folderColorService is null) return items;

        var result = new List<FileSystemItem>(items.Count);
        foreach (var item in items)
        {
            if (item.Type != FileSystemItemType.Directory || item.IsGroupHeader)
            {
                result.Add(item);
                continue;
            }

            var color = _folderColorService.GetColor(item.Path);
            if (color is null)
            {
                result.Add(item);
                continue;
            }

            result.Add(new FileSystemItem
            {
                Name = item.Name,
                Path = item.Path,
                Type = item.Type,
                Size = item.Size,
                LastModified = item.LastModified,
                Created = item.Created,
                Extension = item.Extension,
                IsHidden = item.IsHidden,
                IsReadOnly = item.IsReadOnly,
                TotalSpace = item.TotalSpace,
                FreeSpace = item.FreeSpace,
                IsGroupHeader = item.IsGroupHeader,
                GroupName = item.GroupName,
                FolderColor = color
            });
        }
        return result;
    }

    /// <summary>
    /// Builds the item list for a color-group virtual view: every folder assigned the given
    /// color, gathered from anywhere on disk. Missing folders are skipped.
    /// </summary>
    private Task<IEnumerable<FileSystemItem>> LoadColorGroupItemsAsync(string virtualPath, CancellationToken ct)
    {
        var targetColor = VirtualPaths.GetColorHex(virtualPath);
        if (string.IsNullOrEmpty(targetColor) || _folderColorService is null)
            return Task.FromResult(Enumerable.Empty<FileSystemItem>());

        return Task.Run<IEnumerable<FileSystemItem>>(() =>
        {
            var result = new List<FileSystemItem>();
            foreach (var (folderPath, colorHex) in _folderColorService.GetAllColors())
            {
                ct.ThrowIfCancellationRequested();

                if (!string.Equals(colorHex, targetColor, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!Directory.Exists(folderPath))
                    continue;

                DateTime? modified = null;
                try { modified = Directory.GetLastWriteTime(folderPath); } catch { }

                result.Add(new FileSystemItem
                {
                    Name = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    Path = folderPath,
                    Type = FileSystemItemType.Directory,
                    LastModified = modified,
                    FolderColor = colorHex
                });
            }
            return result;
        }, ct);
    }

    private IEnumerable<FileSystemItem> SortItems(IEnumerable<FileSystemItem> items)
    {
        var list = items.ToList();

        // If items contain group headers (e.g., This PC view), don't sort — they're pre-ordered
        if (list.Any(x => x.IsGroupHeader))
            return list;

        // Name is the stable tie-breaker so items that compare equal on the primary key
        // (e.g. same modified date, same size) always appear in a deterministic order instead
        // of the OS enumeration order, which changes between loads.
        var asc = SortDirection == SortDirection.Ascending;
        var nameComparer = StringComparer.OrdinalIgnoreCase;

        // 1. Primary sort by the selected property, 2. directories first, 3. tie-break by name.
        //    We start with the directories-first key so folders always lead regardless of mode.
        IOrderedEnumerable<FileSystemItem> ordered = list
            .OrderByDescending(x => x.Type == FileSystemItemType.Directory ? 1 : 0);

        ordered = SortMode switch
        {
            FileSortMode.Name => asc
                ? ordered.ThenBy(x => x.Name, nameComparer)
                : ordered.ThenByDescending(x => x.Name, nameComparer),

            FileSortMode.DateModified => asc
                ? ordered.ThenBy(x => x.LastModified ?? DateTime.MinValue)
                : ordered.ThenByDescending(x => x.LastModified ?? DateTime.MinValue),

            FileSortMode.Type => asc
                ? ordered.ThenBy(x => x.Extension ?? "", nameComparer)
                : ordered.ThenByDescending(x => x.Extension ?? "", nameComparer),

            FileSortMode.Size => asc
                ? ordered.ThenBy(x => x.Size ?? 0)
                : ordered.ThenByDescending(x => x.Size ?? 0),

            FileSortMode.Color => asc
                ? ordered.ThenBy(x => string.IsNullOrEmpty(x.FolderColor) ? 1 : 0).ThenBy(x => x.FolderColor ?? "", nameComparer)
                : ordered.ThenBy(x => string.IsNullOrEmpty(x.FolderColor) ? 1 : 0).ThenByDescending(x => x.FolderColor ?? "", nameComparer),

            _ => ordered
        };

        // Final deterministic tie-break by name (ascending) for any items still equal.
        var finalSorted = ordered.ThenBy(x => x.Name, nameComparer).ToList();

        // 3. Apply grouping
        if (GroupMode != FileGroupMode.None)
        {
            return GroupItems(finalSorted);
        }

        return finalSorted;
    }

    private IEnumerable<FileSystemItem> GroupItems(List<FileSystemItem> items)
    {
        return Helpers.FileGroupingHelper.GroupItems(items, GroupMode);
    }

    private async void LoadPreviewAsync(FileSystemItem? item)
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;

        if (item is null ||
            item.Type is not (FileSystemItemType.File or FileSystemItemType.Directory))
        {
            ClearPreview();
            return;
        }

        IsPreviewLoading = true;
        HasPreviewError = false;
        PreviewErrorMessage = null;

        try
        {
            var result = await _previewService.GetPreviewAsync(item, ct);
            ct.ThrowIfCancellationRequested();

            PreviewType = result.Type;
            PreviewTextContent = result.TextContent;
            PreviewImagePath = result.ImagePath;
            PreviewFileName = result.FileName;
            PreviewFileType = result.FileType;
            PreviewFileSize = result.FileSize;
            PreviewLastModified = result.LastModified;
            PreviewFullPath = result.FullPath;
            PreviewImageWidth = result.ImageWidth;
            PreviewImageHeight = result.ImageHeight;

            if (result.ErrorMessage is not null)
            {
                HasPreviewError = true;
                PreviewErrorMessage = result.ErrorMessage;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            HasPreviewError = true;
            PreviewErrorMessage = $"Preview failed: {ex.Message}";
            PreviewType = PreviewType.None;
        }
        finally
        {
            IsPreviewLoading = false;
        }
    }

    private void ClearPreview()
    {
        PreviewType = PreviewType.None;
        PreviewTextContent = null;
        PreviewImagePath = null;
        PreviewFileName = null;
        PreviewFileType = null;
        PreviewFileSize = null;
        PreviewLastModified = null;
        PreviewFullPath = null;
        PreviewImageWidth = null;
        PreviewImageHeight = null;
        HasPreviewError = false;
        PreviewErrorMessage = null;
        IsPreviewLoading = false;
    }

    private void UpdateBreadcrumbs(string path)
    {
        Breadcrumbs.Clear();

        // Handle virtual paths
        if (VirtualPaths.IsVirtual(path))
        {
            Breadcrumbs.Add(new BreadcrumbItem { Name = VirtualPaths.GetDisplayName(path), Path = path, IsLast = true });
            return;
        }

        var segments = new List<BreadcrumbItem>();
        var current = path;

        while (!string.IsNullOrEmpty(current))
        {
            var name = Path.GetFileName(current);
            if (string.IsNullOrEmpty(name)) name = current;
            segments.Add(new BreadcrumbItem { Name = name, Path = current });
            var parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }

        segments.Reverse();
        // Mark the last segment for visual hierarchy
        if (segments.Count > 0)
            segments[^1].IsLast = true;
        foreach (var segment in segments)
            Breadcrumbs.Add(segment);
    }

    private void NotifyNavigationState()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(CanGoUp));
    }

    private async void LoadSidebarAsync()
    {
        // Load pinned favorites from persisted state
        var savedState = await _statePersistence.LoadAsync();
        if (savedState?.Preferences.PinnedFavorites is { Count: > 0 } pinned)
        {
            foreach (var path in pinned)
            {
                if (Directory.Exists(path))
                    _pinnedFavorites.Add(path);
            }
        }

        var persistedGroups = savedState?.SidebarGroups ?? [];

        // FAVORITES group
        var favorites = CreateSystemGroup(SidebarGroupIds.Favorites, "FAVORITES", persistedGroups);
        var quickAccess = _platformService.GetQuickAccessFolders();
        foreach (var item in quickAccess)
            favorites.Items.Add(item);

        // Add user-pinned favorites
        foreach (var path in _pinnedFavorites)
        {
            favorites.Items.Add(new NavigationItem
            {
                Name = Path.GetFileName(path) ?? path,
                Path = path,
                Kind = NavigationItemKind.Favorite,
                Section = NavigationSection.Favorites
            });
        }

        // LOCATIONS group
        var locations = CreateSystemGroup(SidebarGroupIds.Locations, "LOCATIONS", persistedGroups);
        locations.Items.Add(new NavigationItem
        {
            Name = "This PC",
            Path = VirtualPaths.ThisPC,
            Kind = NavigationItemKind.SpecialLocation,
            Section = NavigationSection.Locations
        });

        var drives = await _fileSystemService.GetDrivesAsync();
        foreach (var drive in drives)
        {
            if (drive.Kind != NavigationItemKind.Network)
                locations.Items.Add(drive);
        }

        // NETWORK group
        var network = CreateSystemGroup(SidebarGroupIds.Network, "NETWORK", persistedGroups);
        network.Items.Add(new NavigationItem
        {
            Name = "Network",
            Path = VirtualPaths.Network,
            Kind = NavigationItemKind.SpecialLocation,
            Section = NavigationSection.Network
        });

        // Add network drives to network section
        foreach (var drive in drives)
        {
            if (drive.Kind == NavigationItemKind.Network)
                network.Items.Add(drive);
        }

        // COLORS group: one entry per color currently in use. Clicking navigates to a virtual
        // view that lists every folder with that color.
        var colorsGroup = CreateSystemGroup(SidebarGroupIds.Colors, "COLORS", persistedGroups);
        RefreshColorGroups(colorsGroup);

        // Custom (user-created) groups, restored from persistence
        foreach (var groupState in persistedGroups.Where(g => !g.IsSystem))
        {
            var group = new SidebarGroup
            {
                Id = groupState.Id,
                Name = groupState.Name,
                IsSystem = false,
                IsExpanded = groupState.IsExpanded
            };

            foreach (var itemState in groupState.Items)
            {
                if (string.IsNullOrWhiteSpace(itemState.Path)) continue;
                group.Items.Add(CreateCustomNavigationItem(itemState.Path, itemState.DisplayName));
            }

            SidebarGroups.Add(group);
        }
    }

    /// <summary>
    /// Rebuilds the COLORS sidebar group: one clickable entry per distinct color currently
    /// assigned to at least one folder. Each entry navigates to the color's virtual view.
    /// </summary>
    private void RefreshColorGroups(SidebarGroup? colorsGroup = null)
    {
        var group = colorsGroup ?? FindGroup(SidebarGroupIds.Colors);
        if (group is null || _folderColorService is null) return;

        group.Items.Clear();

        var presetsByHex = _folderColorService.GetPresetColors()
            .GroupBy(p => p.ColorHex, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        // Distinct colors in use, ordered by name/hex for a stable list.
        var usedColors = _folderColorService.GetAllColors().Values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(hex => presetsByHex.TryGetValue(hex, out var n) ? n : hex, StringComparer.OrdinalIgnoreCase);

        foreach (var hex in usedColors)
        {
            var name = presetsByHex.TryGetValue(hex, out var presetName) ? presetName : hex;
            group.Items.Add(new NavigationItem
            {
                Name = name,
                Path = VirtualPaths.ColorGroup(hex),
                Kind = NavigationItemKind.SpecialLocation,
                Section = NavigationSection.Locations,
                ColorHex = hex
            });
        }
    }

    private SidebarGroup CreateSystemGroup(string id, string name, List<SidebarGroupState> persistedGroups)
    {
        var persisted = persistedGroups.FirstOrDefault(g => g.Id == id);
        var group = new SidebarGroup
        {
            Id = id,
            Name = name,
            IsSystem = true,
            IsExpanded = persisted?.IsExpanded ?? true
        };
        SidebarGroups.Add(group);
        return group;
    }

    private static NavigationItem CreateCustomNavigationItem(string path, string displayName)
    {
        return new NavigationItem
        {
            Name = string.IsNullOrWhiteSpace(displayName)
                ? (Path.GetFileName(path) ?? path)
                : displayName,
            Path = path,
            Kind = NavigationItemKind.Custom,
            Section = NavigationSection.Favorites,
            IsAvailable = VirtualPaths.IsVirtual(path) || Directory.Exists(path) || File.Exists(path)
        };
    }

    private SidebarGroup? FindGroup(string id) =>
        SidebarGroups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));

    // --- Sidebar group commands ---

    [RelayCommand]
    private void ToggleGroup(SidebarGroup? group)
    {
        if (group is not null)
            group.IsExpanded = !group.IsExpanded;
    }

    [RelayCommand]
    private void NewGroup()
    {
        BeginGroupDialog(null);
    }

    [RelayCommand]
    private void RenameGroup(SidebarGroup? group)
    {
        if (group is null || group.IsSystem) return;
        BeginGroupDialog(group);
    }

    [RelayCommand]
    private void DeleteGroup(SidebarGroup? group)
    {
        if (group is null || group.IsSystem) return;
        SidebarGroups.Remove(group);
        StatusText = $"Deleted group '{group.Name}'";
    }

    private void BeginGroupDialog(SidebarGroup? group)
    {
        _groupDialogTarget = group;
        GroupDialogTitle = group is null ? "New Group" : "Rename Group";
        GroupDialogName = group?.Name ?? string.Empty;
        GroupDialogConfirmText = group is null ? "Create" : "Save";
        GroupDialogError = string.Empty;
        IsGroupDialogVisible = true;
    }

    [RelayCommand]
    private void ConfirmGroupDialog()
    {
        var name = GroupDialogName?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            GroupDialogError = "Group name cannot be empty.";
            return;
        }

        // Avoid duplicate custom group names (system groups are case-insensitive excluded).
        if (SidebarGroups.Any(g => !g.IsSystem && !ReferenceEquals(g, _groupDialogTarget)
                                   && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            GroupDialogError = "A group with this name already exists.";
            return;
        }

        if (_groupDialogTarget is not null)
        {
            _groupDialogTarget.Name = name;
            StatusText = $"Renamed group to '{name}'";
        }
        else
        {
            var group = new SidebarGroup
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                IsSystem = false,
                IsExpanded = true
            };
            SidebarGroups.Add(group);
            StatusText = $"Created group '{name}'";
        }

        IsGroupDialogVisible = false;
        GroupDialogError = string.Empty;
    }

    [RelayCommand]
    private void CancelGroupDialog()
    {
        IsGroupDialogVisible = false;
        GroupDialogError = string.Empty;
        _groupDialogTarget = null;
    }

    [RelayCommand]
    private void RemoveFromGroup(NavigationItem? item)
    {
        if (item is null) return;

        var group = SidebarGroups.FirstOrDefault(g => g.Items.Contains(item));
        if (group is null) return;

        group.Items.Remove(item);
        StatusText = $"Removed '{item.Name}' from group '{group.Name}'";
    }

    [RelayCommand]
    private void MoveGroupUp(SidebarGroup? group)
    {
        MoveGroup(group, -1);
    }

    [RelayCommand]
    private void MoveGroupDown(SidebarGroup? group)
    {
        MoveGroup(group, +1);
    }

    private void MoveGroup(SidebarGroup? group, int direction)
    {
        if (group is null || group.IsSystem) return;

        var index = SidebarGroups.IndexOf(group);
        if (index < 0) return;

        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= SidebarGroups.Count) return;

        // Keep system groups pinned to the top: custom groups cannot move above them.
        if (SidebarGroups[newIndex].IsSystem) return;

        SidebarGroups.Move(index, newIndex);
    }

    /// <summary>Adds a filesystem path to a custom group (used by the "Add to Sidebar Group" menu).</summary>
    public void AddPathToGroup(string? path, SidebarGroup group)
    {
        if (string.IsNullOrWhiteSpace(path) || group.IsSystem) return;
        if (group.Items.Any(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))) return;

        group.Items.Add(CreateCustomNavigationItem(path, Path.GetFileName(path) ?? path));
        StatusText = $"Added '{Path.GetFileName(path)}' to group '{group.Name}'";
    }

    /// <summary>Adds the currently selected folder to the given custom group.</summary>
    [RelayCommand]
    private void AddToGroup(SidebarGroup? group)
    {
        if (group is null) return;
        AddPathToGroup(SelectedItem?.Path, group);
    }

    // --- Sorting & Grouping Commands ---

    [RelayCommand]
    private void SetSortMode(FileSortMode mode)
    {
        SortMode = mode;
        if (_tabService.ActiveTab != null)
        {
            _tabService.ActiveTab.SortMode = mode;
        }
        NotifySortIndicators();
        LoadDirectory(CurrentPath);
    }

    /// <summary>
    /// Sorts by the given column (from a details-view header click). Clicking the current sort
    /// column toggles the direction; clicking a different column sorts it ascending.
    /// </summary>
    [RelayCommand]
    private void SortByColumn(FileSortMode mode)
    {
        if (SortMode == mode)
            SortDirection = SortDirection == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        else
        {
            SortMode = mode;
            SortDirection = SortDirection.Ascending;
        }

        if (_tabService.ActiveTab is { } tab)
        {
            tab.SortMode = SortMode;
            tab.SortDirection = SortDirection;
        }

        NotifySortIndicators();
        LoadDirectory(CurrentPath);
    }

    // --- Details-view header sort indicators (arrow shown on the active column) ---
    public bool IsSortedByName => SortMode == FileSortMode.Name;
    public bool IsSortedByModified => SortMode == FileSortMode.DateModified;
    public bool IsSortedBySize => SortMode == FileSortMode.Size;
    public bool IsSortedByColor => SortMode == FileSortMode.Color;
    public string SortDirectionGlyph => SortDirection == SortDirection.Ascending ? "\u25B2" : "\u25BC"; // ▲ / ▼

    private void NotifySortIndicators()
    {
        OnPropertyChanged(nameof(IsSortedByName));
        OnPropertyChanged(nameof(IsSortedByModified));
        OnPropertyChanged(nameof(IsSortedBySize));
        OnPropertyChanged(nameof(IsSortedByColor));
        OnPropertyChanged(nameof(SortDirectionGlyph));
    }

    [RelayCommand]
    private void SetSortDirection(SortDirection direction)
    {
        SortDirection = direction;
        if (_tabService.ActiveTab != null)
        {
            _tabService.ActiveTab.SortDirection = direction;
        }
        NotifySortIndicators();
        LoadDirectory(CurrentPath);
    }

    [RelayCommand]
    private void SetGroupMode(FileGroupMode mode)
    {
        GroupMode = mode;
        if (_tabService.ActiveTab != null)
        {
            _tabService.ActiveTab.GroupMode = mode;
        }
        LoadDirectory(CurrentPath);
    }

    [RelayCommand]
    private void ToggleShowHiddenFiles()
    {
        ShowHiddenFiles = !ShowHiddenFiles;
        if (_tabService.ActiveTab != null)
        {
            _tabService.ActiveTab.ShowHiddenFiles = ShowHiddenFiles;
        }
        LoadDirectory(CurrentPath);
    }

    [RelayCommand]
    private void ToggleShowItemInfo()
    {
        ShowItemInfo = !ShowItemInfo;
    }

    [RelayCommand]
    private void SetThemeLight() => CurrentTheme = ThemeMode.Light;

    [RelayCommand]
    private void SetThemeDark() => CurrentTheme = ThemeMode.Dark;

    [RelayCommand]
    private void SetThemeSystem() => CurrentTheme = ThemeMode.System;

    [RelayCommand]
    private void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
    }

    // --- Search logic ---
    
    partial void OnSearchQueryChanged(string value)
    {
        if (_tabService.ActiveTab != null)
        {
            _tabService.ActiveTab.SearchQuery = value;
        }
        
        // Cancel any previous search
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();

        if (string.IsNullOrWhiteSpace(value))
        {
            // Immediately clear search and restore directory view
            ExecuteSearch(value, _searchCts.Token);
        }
        else
        {
            // Debounce: wait briefly before starting search to avoid cancelling on every keystroke
            var cts = _searchCts;
            _ = DebounceSearchAsync(value, cts);
        }
    }

    private async Task DebounceSearchAsync(string query, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(300, cts.Token);
            ExecuteSearch(query, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when user types quickly
        }
    }

    private async void ExecuteSearch(string query, CancellationToken? token = null)
    {
        // Use the token captured when this search was launched, not the current field,
        // so a newer search's CTS can't be mistaken for this one (cancellation race).
        var ct = token ?? _searchCts?.Token ?? CancellationToken.None;

        if (string.IsNullOrWhiteSpace(query))
        {
            IsSearchActive = false;
            if (_tabService.ActiveTab != null)
                _tabService.ActiveTab.SearchActive = false;
            
            LoadDirectory(CurrentPath);
            return;
        }

        // Cannot search in virtual paths — use home directory as fallback
        var searchPath = CurrentPath;
        if (string.IsNullOrEmpty(searchPath) || VirtualPaths.IsVirtual(searchPath))
            searchPath = _platformService.HomePath;

        IsSearchActive = true;
        if (_tabService.ActiveTab != null)
            _tabService.ActiveTab.SearchActive = true;

        IsLoading = true;
        IsEmpty = false;
        Items.Clear();
        ItemCount = 0;
        StatusText = "Searching...";

        try
        {
            var searchResults = new List<FileSystemItem>();

            // First: quickly show matches from immediate directory
            try
            {
                var immediateItems = await _fileSystemService.GetItemsAsync(searchPath, ct);
                var immediateMatches = immediateItems
                    .Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (immediateMatches.Count > 0)
                {
                    searchResults.AddRange(immediateMatches);
                    UpdateSearchUI(searchResults);
                    StatusText = $"{searchResults.Count} results";
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { /* Directory might not be accessible */ }

            // Then: recursive search for deeper results
            await foreach (var item in _searchService.SearchAsync(searchPath, query, ct))
            {
                // Skip items we already found in immediate directory
                if (searchResults.Any(r => string.Equals(r.Path, item.Path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                searchResults.Add(item);
                
                if (searchResults.Count % 20 == 0)
                {
                    UpdateSearchUI(searchResults);
                    StatusText = $"{searchResults.Count} results...";
                }
            }
            
            UpdateSearchUI(searchResults);
            
            IsEmpty = searchResults.Count == 0;
            StatusText = IsEmpty ? "No items found" : $"{searchResults.Count} results";
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Search failed: {ex.Message}";
            StatusText = "Error";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateSearchUI(List<FileSystemItem> results)
    {
        Items.Clear();
        var sorted = SortItems(results);
        foreach (var item in sorted)
            Items.Add(item);
        ItemCount = Items.Count;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty; // Triggers OnSearchQueryChanged
    }
}
