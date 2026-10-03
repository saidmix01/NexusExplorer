using System.ComponentModel;
using System.Runtime.CompilerServices;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Core.Models;

/// <summary>
/// Represents a tab in the file explorer with its own independent navigation state.
/// Each tab maintains its own back/forward history, current path, and terminal state.
/// </summary>
public sealed class TabItem : INotifyPropertyChanged
{
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();

    private string _title;
    private bool _isActive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; } = Guid.NewGuid().ToString();

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public string CurrentPath { get; private set; }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    public FileSystemItem? SelectedItem { get; set; }

    // --- Terminal state ---
    public ITerminalSession? TerminalSession { get; set; }
    public bool IsTerminalOpen { get; set; }
    public string? TerminalWorkingDirectory { get; set; }
    public string? TerminalError { get; set; }

    /// <summary>
    /// Custom folder color hex for the current path (e.g. "#E05555").
    /// Null means default folder color.
    /// Used to tint the tab border to match the folder's assigned color.
    /// </summary>
    public string? FolderColor
    {
        get => _folderColor;
        set
        {
            if (_folderColor != value)
            {
                _folderColor = value;
                OnPropertyChanged();
            }
        }
    }
    private string? _folderColor;

    // --- Layout state ---
    public LayoutMode LayoutMode { get; set; } = LayoutMode.ExplorerOnly;
    public SplitOrientation SplitOrientation { get; set; } = SplitOrientation.Horizontal;
    public double SplitRatio { get; set; } = 0.6; // 60% explorer, 40% terminal
    public ExplorerViewMode ViewMode { get; set; } = ExplorerViewMode.Details;

    // --- Search, Sort, and Group state ---
    public FileSortMode SortMode { get; set; } = FileSortMode.Name;
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;
    public FileGroupMode GroupMode { get; set; } = FileGroupMode.None;
    public string SearchQuery { get; set; } = string.Empty;
    public bool SearchActive { get; set; } = false;
    public bool ShowHiddenFiles { get; set; } = false;

    public bool CanGoBack => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;
    public bool CanGoUp => !string.IsNullOrEmpty(CurrentPath)
                           && !VirtualPaths.IsVirtual(CurrentPath)
                           && !string.IsNullOrEmpty(Path.GetDirectoryName(CurrentPath));

    /// <summary>Gets a snapshot of the back navigation stack (most recent first) for persistence.</summary>
    public IReadOnlyList<string> BackStackSnapshot => _backStack.ToList();

    /// <summary>Gets a snapshot of the forward navigation stack (most recent first) for persistence.</summary>
    public IReadOnlyList<string> ForwardStackSnapshot => _forwardStack.ToList();

    public TabItem(string path, string? title = null)
    {
        CurrentPath = path;
        _title = title ?? ResolveTitle(path);
    }

    private static string ResolveTitle(string path)
    {
        if (VirtualPaths.IsVirtual(path))
            return VirtualPaths.GetDisplayName(path);

        // Path.GetFileName returns "" (never null) for roots like "C:\"; fall back to the path.
        return Path.GetFileName(path) is { Length: > 0 } name ? name : path;
    }

    /// <summary>
    /// Creates a tab with restored navigation history (used for session restore).
    /// </summary>
    public TabItem(string path, string? title, IEnumerable<string>? backStack, IEnumerable<string>? forwardStack)
        : this(path, title)
    {
        if (backStack is not null)
        {
            foreach (var p in backStack)
                _backStack.Push(p);
        }
        if (forwardStack is not null)
        {
            foreach (var p in forwardStack)
                _forwardStack.Push(p);
        }
    }

    public void NavigateTo(string path)
    {
        if (string.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase))
            return;

        if (!string.IsNullOrEmpty(CurrentPath))
            _backStack.Push(CurrentPath);

        _forwardStack.Clear();
        CurrentPath = path;
        UpdateTitle();
    }

    public string? GoBack()
    {
        if (!CanGoBack) return null;

        _forwardStack.Push(CurrentPath);
        CurrentPath = _backStack.Pop();
        UpdateTitle();
        return CurrentPath;
    }

    public string? GoForward()
    {
        if (!CanGoForward) return null;

        _backStack.Push(CurrentPath);
        CurrentPath = _forwardStack.Pop();
        UpdateTitle();
        return CurrentPath;
    }

    public string? GoUp()
    {
        if (!CanGoUp) return null;

        var parent = Path.GetDirectoryName(CurrentPath);
        if (string.IsNullOrEmpty(parent)) return null;

        NavigateTo(parent);
        return CurrentPath;
    }

    public string Refresh() => CurrentPath;

    /// <summary>
    /// Disposes the terminal session if one exists.
    /// </summary>
    public void DisposeTerminal()
    {
        if (TerminalSession is not null)
        {
            TerminalSession.Dispose();
            TerminalSession = null;
        }
        IsTerminalOpen = false;
        TerminalWorkingDirectory = null;
        TerminalError = null;
    }

    private void UpdateTitle()
    {
        if (VirtualPaths.IsVirtual(CurrentPath))
        {
            Title = VirtualPaths.GetDisplayName(CurrentPath);
        }
        else
        {
            Title = Path.GetFileName(CurrentPath) is { Length: > 0 } name ? name : CurrentPath;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
