using Microsoft.Extensions.Logging;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Infrastructure.FileSystem;

/// <summary>
/// Watches a directory for file system changes using System.IO.FileSystemWatcher.
/// Debounces rapid notifications to avoid flooding the UI with refreshes.
/// </summary>
public sealed class FileWatcherService : IFileWatcherService
{
    private readonly ILogger<FileWatcherService> _logger;
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private Timer? _restartTimer;
    private volatile int _suppressCount;
    private readonly object _lock = new();
    private const int DebounceDelayMs = 300;

    public string? CurrentPath { get; private set; }

    public event EventHandler? Changed;

    public FileWatcherService(ILogger<FileWatcherService> logger)
    {
        _logger = logger;
    }

    public void Watch(string path)
    {
        lock (_lock)
        {
            if (string.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase))
                return;

            StopWatchingInternal();

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            try
            {
                _watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size
                                 | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _watcher.Created += OnFileSystemEvent;
                _watcher.Deleted += OnFileSystemEvent;
                _watcher.Renamed += OnFileSystemEvent;
                _watcher.Changed += OnFileSystemEvent;
                _watcher.Error += OnWatcherError;

                CurrentPath = path;
                _logger.LogDebug("Started watching: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start watching: {Path}", path);
                StopWatchingInternal();
            }
        }
    }

    public void StopWatching()
    {
        lock (_lock)
        {
            StopWatchingInternal();
        }
    }

    public IDisposable Suppress()
    {
        Interlocked.Increment(ref _suppressCount);
        return new SuppressToken(this);
    }

    private void StopWatchingInternal()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileSystemEvent;
            _watcher.Deleted -= OnFileSystemEvent;
            _watcher.Renamed -= OnFileSystemEvent;
            _watcher.Changed -= OnFileSystemEvent;
            _watcher.Error -= OnWatcherError;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _restartTimer?.Dispose();
        _restartTimer = null;
        CurrentPath = null;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (_suppressCount > 0)
            return;

        // Debounce: reset the timer on each event, fire only after the delay
        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(OnDebounceElapsed, null, DebounceDelayMs, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        if (_suppressCount > 0)
            return;

        _logger.LogDebug("File system change detected, raising Changed event");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _logger.LogWarning(ex, "FileSystemWatcher error for: {Path}", CurrentPath);

        // Try to restart the watcher
        lock (_lock)
        {
            var path = CurrentPath;
            StopWatchingInternal();

            if (!string.IsNullOrEmpty(path))
            {
                // Delay restart slightly to avoid tight loops. Use a dedicated timer so a
                // normal debounce event can't dispose/replace the restart timer (which would
                // leave the watcher permanently stopped).
                _restartTimer?.Dispose();
                _restartTimer = new Timer(_ =>
                {
                    if (Directory.Exists(path))
                        Watch(path);
                }, null, 1000, Timeout.Infinite);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            StopWatchingInternal();
        }
    }

    private sealed class SuppressToken : IDisposable
    {
        private readonly FileWatcherService _service;
        private bool _disposed;

        public SuppressToken(FileWatcherService service)
        {
            _service = service;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Interlocked.Decrement(ref _service._suppressCount);
        }
    }
}
