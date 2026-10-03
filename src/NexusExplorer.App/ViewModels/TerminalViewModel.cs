using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusExplorer.App.Terminal;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// ViewModel for the terminal panel. Manages session lifecycle, I/O buffering,
/// and working directory synchronization for the active tab.
/// </summary>
public partial class TerminalViewModel : ObservableObject
{
    private readonly ITerminalSessionService _terminalSessionService;
    private RichTerminalBuffer _buffer = new();
    private TerminalControl? _control;

    /// <summary>
    /// Fired when the terminal's working directory changes (detected via OSC 9;9).
    /// The MainWindowViewModel hooks this to sync Explorer navigation.
    /// </summary>
    public event Action<string>? TerminalDirectoryChanged;

    /// <summary>
    /// Fired when the terminal is closed via the X button. The MainWindowViewModel
    /// hooks this to collapse the terminal panel back to Explorer-only layout.
    /// </summary>
    public event Action? TerminalCloseRequested;

    [ObservableProperty]
    private bool _isTerminalOpen;

    [ObservableProperty]
    private bool _isSessionActive;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _shellName = "PowerShell";

    private TabItem? _currentTab;
    private ITerminalSession? _currentSession;

    // Tracks when the shell last produced output — used to detect if a process is actively running
    private DateTime _lastOutputTime = DateTime.MinValue;

    /// <summary>
    /// True if the shell appears to be busy (received output within the last 800ms).
    /// Used to skip directory sync when a process is actively running.
    /// </summary>
    public bool IsShellBusy => IsSessionActive && (DateTime.UtcNow - _lastOutputTime).TotalMilliseconds < 800;

    public TerminalViewModel(ITerminalSessionService terminalSessionService)
    {
        _terminalSessionService = terminalSessionService;
    }

    public TerminalViewModel()
    {
        _terminalSessionService = null!;
    }

    /// <summary>
    /// Called by the View to attach the rendering control.
    /// </summary>
    public void AttachControl(TerminalControl control)
    {
        _control = control;
        _control.SetBuffer(_buffer);
        _control.SetTheme(TerminalTheme.LinuxDark);

        // If the control already has a known size, resize the buffer to match
        if (control.CellWidth > 0 && control.CellHeight > 0 && control.Bounds.Width > 0 && control.Bounds.Height > 0)
        {
            var cols = Terminal.TerminalSizeHelper.CalculateColumns(control.Bounds.Width, control.CellWidth);
            var rows = Terminal.TerminalSizeHelper.CalculateRows(control.Bounds.Height, control.CellHeight);
            _buffer.Resize(cols, rows);
        }

        // Wire CWD detection from terminal output
        _buffer.WorkingDirectoryChanged += path =>
        {
            WorkingDirectory = path;
            if (_currentTab is not null)
                _currentTab.TerminalWorkingDirectory = path;
            TerminalDirectoryChanged?.Invoke(path);
        };
    }

    /// <summary>
    /// Switches the terminal view to the given tab's state.
    /// </summary>
    public void SwitchToTab(TabItem tab)
    {
        DetachSession();
        _currentTab = tab;
        IsTerminalOpen = tab.IsTerminalOpen;

        if (tab.TerminalSession is not null && tab.TerminalSession.IsRunning)
        {
            _currentSession = tab.TerminalSession;
            IsSessionActive = true;
            WorkingDirectory = tab.TerminalWorkingDirectory ?? tab.CurrentPath;
            HasError = false;
            ErrorMessage = null;

            // Detach first to avoid double-subscription (duplicated output) when re-attaching.
            _currentSession.OutputReceived -= OnOutputReceived;
            _currentSession.OutputReceived += OnOutputReceived;
            _currentSession.ProcessExited -= OnProcessExited;
            _currentSession.ProcessExited += OnProcessExited;

            if (tab.TerminalError is not null)
            {
                HasError = true;
                ErrorMessage = tab.TerminalError;
            }
        }
        else
        {
            _currentSession = null;
            IsSessionActive = false;
            WorkingDirectory = tab.CurrentPath;
            HasError = tab.TerminalError is not null;
            ErrorMessage = tab.TerminalError;
        }

        // Each tab gets a fresh buffer view (we don't persist rendered output per tab for now)
        _buffer.Clear();
        _control?.NotifyOutputChanged();
    }

    [RelayCommand]
    public async Task OpenTerminalAsync()
    {
        if (_currentTab is null) return;

        IsTerminalOpen = true;
        _currentTab.IsTerminalOpen = true;
        HasError = false;
        ErrorMessage = null;

        if (_currentTab.TerminalSession is not null && _currentTab.TerminalSession.IsRunning)
        {
            _currentSession = _currentTab.TerminalSession;
            // Detach first to avoid double-subscription (duplicated output).
            _currentSession.OutputReceived -= OnOutputReceived;
            _currentSession.OutputReceived += OnOutputReceived;
            _currentSession.ProcessExited -= OnProcessExited;
            _currentSession.ProcessExited += OnProcessExited;
            IsSessionActive = true;
            return;
        }

        await CreateSessionAsync(_currentTab.CurrentPath);
    }

    /// <summary>
    /// Opens the terminal at a specific path. If a session is already active,
    /// syncs the working directory. Otherwise creates a new session at that path.
    /// </summary>
    public async Task OpenTerminalAtPathAsync(string path)
    {
        if (_currentTab is null) return;

        IsTerminalOpen = true;
        _currentTab.IsTerminalOpen = true;
        HasError = false;
        ErrorMessage = null;

        if (_currentTab.TerminalSession is not null && _currentTab.TerminalSession.IsRunning)
        {
            // Session already exists — just sync the working directory
            _currentSession = _currentTab.TerminalSession;
            _currentSession.OutputReceived -= OnOutputReceived;
            _currentSession.OutputReceived += OnOutputReceived;
            _currentSession.ProcessExited -= OnProcessExited;
            _currentSession.ProcessExited += OnProcessExited;
            IsSessionActive = true;
            await SyncWorkingDirectoryAsync(path);
            return;
        }

        await CreateSessionAsync(path);
    }

    [RelayCommand]
    public void CloseTerminal()
    {
        IsTerminalOpen = false;
        if (_currentTab is not null)
            _currentTab.IsTerminalOpen = false;
    }

    [RelayCommand]
    public async Task TerminateTerminalAsync()
    {
        if (_currentSession is not null)
        {
            DetachSession();
            await _currentSession.CloseAsync();
            _currentSession.Dispose();
            _currentSession = null;
        }

        if (_currentTab is not null)
        {
            _currentTab.TerminalSession = null;
            _currentTab.IsTerminalOpen = false;
            _currentTab.TerminalWorkingDirectory = null;
        }

        IsTerminalOpen = false;
        IsSessionActive = false;
        // Keep the buffer visible — just append a separator so the user can see what ran
        _buffer.Write("\r\n\x1b[90m─── Terminal closed. Open a new terminal with the + button. ───\x1b[0m\r\n");
        _control?.NotifyOutputChanged();

        // Ask the host window to collapse the terminal panel.
        TerminalCloseRequested?.Invoke();
    }

    [RelayCommand]
    public async Task NewTerminalAsync()
    {
        if (_currentTab is null) return;

        if (_currentSession is not null)
        {
            DetachSession();
            await _currentSession.CloseAsync();
            _currentSession.Dispose();
            _currentSession = null;
            if (_currentTab.TerminalSession is not null)
                _currentTab.TerminalSession = null;
        }

        _buffer.Clear();
        _control?.NotifyOutputChanged();
        await CreateSessionAsync(_currentTab.CurrentPath);
    }

    [RelayCommand]
    public void ClearOutput()
    {
        _buffer.Clear();
        _control?.NotifyOutputChanged();
    }

    public async Task SendInputAsync(string text)
    {
        if (_currentSession is null || !_currentSession.IsRunning) return;
        try { await _currentSession.WriteInputAsync(text); }
        catch (Exception ex) { HasError = true; ErrorMessage = $"Input failed: {ex.Message}"; }
    }

    public async Task SendKeyAsync(byte[] data)
    {
        if (_currentSession is null || !_currentSession.IsRunning) return;
        try { await _currentSession.WriteInputAsync(data.AsMemory()); }
        catch (Exception ex) { HasError = true; ErrorMessage = $"Input failed: {ex.Message}"; }
    }

    public async Task SyncWorkingDirectoryAsync(string path)
    {
        if (_currentSession is null || !_currentSession.IsRunning) return;
        if (string.Equals(WorkingDirectory, path, StringComparison.OrdinalIgnoreCase)) return;

        var escapedPath = path.Replace("'", "''");
        var command = $"Set-Location -LiteralPath '{escapedPath}'\r";

        try
        {
            await _currentSession.WriteInputAsync(command);
            WorkingDirectory = path;
            if (_currentTab is not null)
                _currentTab.TerminalWorkingDirectory = path;
        }
        catch (Exception ex) { HasError = true; ErrorMessage = $"Sync failed: {ex.Message}"; }
    }

    public async Task ResizeAsync(int columns, int rows)
    {
        _buffer.Resize(columns, rows);
        if (_currentSession is null || !_currentSession.IsRunning) return;
        try { await _currentSession.ResizeAsync(columns, rows); } catch { }
    }

    public void DisposeTabTerminal(TabItem tab)
    {
        if (tab.TerminalSession is not null)
        {
            if (_currentSession == tab.TerminalSession)
                DetachSession();
            tab.TerminalSession.Dispose();
            tab.TerminalSession = null;
        }
    }

    private async Task CreateSessionAsync(string workingDirectory)
    {
        if (!_terminalSessionService.IsSupported)
        {
            HasError = true;
            ErrorMessage = "Terminal is not supported on this platform.";
            if (_currentTab is not null) _currentTab.TerminalError = ErrorMessage;
            return;
        }

        var shellPath = _terminalSessionService.GetDefaultShellPath();
        if (shellPath is null)
        {
            HasError = true;
            ErrorMessage = "No supported shell found (PowerShell required).";
            if (_currentTab is not null) _currentTab.TerminalError = ErrorMessage;
            return;
        }

        var shellFileName = Path.GetFileNameWithoutExtension(shellPath).ToLowerInvariant();
        ShellName = shellFileName switch
        {
            "pwsh" => "PowerShell 7",
            "powershell" => "Windows PowerShell",
            _ => shellFileName
        };

        try
        {
            var options = new TerminalSessionOptions
            {
                WorkingDirectory = workingDirectory,
                ShellPath = shellPath,
                Columns = _buffer.Columns,
                Rows = _buffer.Rows
            };

            var session = await _terminalSessionService.CreateSessionAsync(options);
            _currentSession = session;
            _currentSession.OutputReceived += OnOutputReceived;
            _currentSession.ProcessExited += OnProcessExited;

            if (_currentTab is not null)
            {
                _currentTab.TerminalSession = session;
                _currentTab.TerminalWorkingDirectory = workingDirectory;
                _currentTab.TerminalError = null;
            }

            WorkingDirectory = workingDirectory;
            IsSessionActive = true;
            HasError = false;
            ErrorMessage = null;

            // Inject a prompt hook that reports CWD via OSC 9;9 after each command.
            // This enables Terminal→Explorer directory synchronization.
            _ = InjectCwdReportingAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to start terminal: {ex.Message}";
            if (_currentTab is not null) _currentTab.TerminalError = ErrorMessage;
            IsSessionActive = false;
        }
    }

    private void OnOutputReceived(object? sender, ReadOnlyMemory<byte> data)
    {
        _lastOutputTime = DateTime.UtcNow;
        var rawText = Encoding.UTF8.GetString(data.Span);
        Dispatcher.UIThread.Post(() =>
        {
            _buffer.Write(rawText);
            _control?.NotifyOutputChanged();
        });
    }

    private void OnProcessExited(object? sender, int exitCode)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsSessionActive = false;
            _buffer.Write($"\r\n[Process exited with code {exitCode}]\r\n");
            _control?.NotifyOutputChanged();
        });
    }

    private void DetachSession()
    {
        if (_currentSession is not null)
        {
            _currentSession.OutputReceived -= OnOutputReceived;
            _currentSession.ProcessExited -= OnProcessExited;
        }
    }

    /// <summary>
    /// Injects a PowerShell prompt function that emits OSC 9;9 with the current directory
    /// after each command execution. This is how Windows Terminal detects CWD changes.
    /// The command is hidden from the user by clearing the screen after injection.
    /// </summary>
    private async Task InjectCwdReportingAsync()
    {
        if (_currentSession is null || !_currentSession.IsRunning) return;

        // Wait for the shell to fully initialize
        await Task.Delay(600);

        // Set up a prompt function that emits OSC 9;9;CWD (same protocol as Windows Terminal)
        // Then clear the screen so the setup command is not visible to the user
        const string promptHook = """
            function global:prompt { $p = $executionContext.SessionState.Path.CurrentLocation.Path; "$([char]27)]9;9;$p$([char]7)PS $p> " }
            """;

        try
        {
            await _currentSession.WriteInputAsync(promptHook.Trim() + "\r");
            // Wait briefly for the command to execute, then clear
            await Task.Delay(300);
            await _currentSession.WriteInputAsync("cls\r");
        }
        catch
        {
            // Non-critical — sync just won't work if this fails
        }
    }
}
