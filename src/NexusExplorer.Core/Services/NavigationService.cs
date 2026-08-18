using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Core.Services;

/// <summary>
/// Manages navigation history and path transitions.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly ILogger<NavigationService> _logger;
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private string _currentPath = string.Empty;

    public NavigationService(ILogger<NavigationService>? logger = null)
    {
        _logger = logger ?? NullLogger<NavigationService>.Instance;
    }

    public string CurrentPath => _currentPath;
    public bool CanGoBack => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;
    public bool CanGoUp => !string.IsNullOrEmpty(_currentPath)
                           && Path.GetDirectoryName(_currentPath) is not null;

    public event EventHandler<string>? Navigated;

    public void NavigateTo(string path)
    {
        if (string.Equals(_currentPath, path, StringComparison.OrdinalIgnoreCase))
            return;

        _logger.LogDebug("Navigating to: {Path}", path);

        if (!string.IsNullOrEmpty(_currentPath))
            _backStack.Push(_currentPath);

        _forwardStack.Clear();
        _currentPath = path;
        Navigated?.Invoke(this, path);
    }

    public void GoBack()
    {
        if (!CanGoBack) return;

        _forwardStack.Push(_currentPath);
        _currentPath = _backStack.Pop();
        Navigated?.Invoke(this, _currentPath);
    }

    public void GoForward()
    {
        if (!CanGoForward) return;

        _backStack.Push(_currentPath);
        _currentPath = _forwardStack.Pop();
        Navigated?.Invoke(this, _currentPath);
    }

    public void GoUp()
    {
        var parent = Path.GetDirectoryName(_currentPath);
        if (!string.IsNullOrEmpty(parent))
            NavigateTo(parent);
    }

    public void Refresh()
    {
        if (!string.IsNullOrEmpty(_currentPath))
            Navigated?.Invoke(this, _currentPath);
    }
}
