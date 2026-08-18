using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Windows implementation of <see cref="IGlobalHotkeyService"/> using the Win32
/// RegisterHotKey/UnregisterHotKey APIs. A dedicated background thread runs a message
/// loop and receives WM_HOTKEY — no keyboard hooks, no polling, no window creation.
/// </summary>
public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_QUIT = 0x0012;
    private const uint WM_APP_COMMAND = 0x8000;
    private const uint PM_NOREMOVE = 0x0000;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20,
        ["Enter"] = 0x0D,
        ["Return"] = 0x0D,
        ["Escape"] = 0x1B,
        ["Esc"] = 0x1B,
        ["Tab"] = 0x09,
        ["Backspace"] = 0x08,
        ["Delete"] = 0x2E,
        ["Del"] = 0x2E,
        ["Insert"] = 0x2D,
        ["Ins"] = 0x2D,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Up"] = 0x26,
        ["Down"] = 0x28,
        ["Left"] = 0x25,
        ["Right"] = 0x27,
        ["OemTilde"] = 0xC0,
        ["OemMinus"] = 0xBD,
        ["OemPlus"] = 0xBB,
        ["OemComma"] = 0xBC,
        ["OemPeriod"] = 0xBE,
    };

    private readonly ILogger<WindowsGlobalHotkeyService> _logger;
    private readonly object _sync = new();
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly Dictionary<int, GlobalHotkey> _hotkeysById = new();
    private readonly Dictionary<string, int> _idByHotkeyId = new();
    private readonly ManualResetEventSlim _ready = new(false);

    private Thread? _thread;
    private uint _threadId;
    private int _nextId = 1;
    private volatile bool _disposed;

    public WindowsGlobalHotkeyService(ILogger<WindowsGlobalHotkeyService> logger)
    {
        _logger = logger;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public event Action<string>? HotkeyTriggered;

    public bool Register(GlobalHotkey hotkey)
    {
        if (!IsSupported || _disposed) return false;

        if (!TryParseShortcut(hotkey.Shortcut, out var modifiers, out var vk))
        {
            _logger.LogWarning("Cannot register global hotkey '{Shortcut}': invalid shortcut.", hotkey.Shortcut);
            return false;
        }

        EnsureStarted();

        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue(() =>
        {
            lock (_sync)
            {
                if (_idByHotkeyId.ContainsKey(hotkey.Id))
                {
                    result.TrySetResult(false);
                    return;
                }

                var id = _nextId++;
                if (RegisterHotKey(IntPtr.Zero, id, modifiers, vk))
                {
                    _hotkeysById[id] = hotkey;
                    _idByHotkeyId[hotkey.Id] = id;
                    _logger.LogInformation("Registered global shortcut: {Shortcut}.", hotkey.Shortcut);
                    result.TrySetResult(true);
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();
                    _logger.LogError("Failed to register global shortcut {Shortcut}. Win32 error: {Error}.",
                        hotkey.Shortcut, error);
                    result.TrySetResult(false);
                }
            }
        });

        PostThreadMessage(_threadId, WM_APP_COMMAND, IntPtr.Zero, IntPtr.Zero);
        return result.Task.Wait(TimeSpan.FromSeconds(2)) && result.Task.Result;
    }

    public void Unregister(string id)
    {
        if (!IsSupported || _thread is null) return;

        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue(() =>
        {
            lock (_sync)
            {
                if (_idByHotkeyId.Remove(id, out var hotkeyId))
                {
                    UnregisterHotKey(IntPtr.Zero, hotkeyId);
                    _hotkeysById.Remove(hotkeyId);
                    _logger.LogInformation("Unregistered global shortcut '{Id}'.", id);
                }
                result.TrySetResult(true);
            }
        });

        PostThreadMessage(_threadId, WM_APP_COMMAND, IntPtr.Zero, IntPtr.Zero);
        result.Task.Wait(TimeSpan.FromSeconds(2));
    }

    public void UnregisterAll()
    {
        if (!IsSupported || _thread is null) return;

        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue(() =>
        {
            lock (_sync)
            {
                foreach (var hotkeyId in _idByHotkeyId.Values)
                    UnregisterHotKey(IntPtr.Zero, hotkeyId);

                _idByHotkeyId.Clear();
                _hotkeysById.Clear();
                result.TrySetResult(true);
            }
        });

        PostThreadMessage(_threadId, WM_APP_COMMAND, IntPtr.Zero, IntPtr.Zero);
        result.Task.Wait(TimeSpan.FromSeconds(2));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_thread is not null)
        {
            PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _thread.Join(2000);
        }

        _ready.Dispose();
    }

    private void EnsureStarted()
    {
        if (_thread is not null) return;

        _thread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "NexusExplorer.GlobalHotkey",
        };
        _thread.Start();
        _ready.Wait();
    }

    private void MessageLoop()
    {
        _threadId = GetCurrentThreadId();

        // Create this thread's message queue before another thread posts to it.
        PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
        _ready.Set();

        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_HOTKEY)
            {
                GlobalHotkey? hotkey;
                lock (_sync)
                {
                    _hotkeysById.TryGetValue(msg.wParam.ToInt32(), out hotkey);
                }

                if (hotkey is not null)
                {
                    _logger.LogInformation("Open Nexus shortcut triggered.");
                    HotkeyTriggered?.Invoke(hotkey.Id);
                }
            }
            else if (msg.message == WM_APP_COMMAND)
            {
                while (_commands.TryDequeue(out var command))
                    command();
            }
        }

        // WM_QUIT received: release any remaining hotkeys.
        lock (_sync)
        {
            foreach (var hotkeyId in _idByHotkeyId.Values)
                UnregisterHotKey(IntPtr.Zero, hotkeyId);

            _idByHotkeyId.Clear();
            _hotkeysById.Clear();
        }
    }

    private static bool TryParseShortcut(string shortcut, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(shortcut)) return false;

        var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false; // at least one modifier + one key

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var token = parts[i];
            if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Control", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_CONTROL;
            else if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_ALT;
            else if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_SHIFT;
            else if (token.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                     token.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                     token.Equals("Meta", StringComparison.OrdinalIgnoreCase) ||
                     token.Equals("Cmd", StringComparison.OrdinalIgnoreCase) ||
                     token.Equals("Super", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_WIN;
            else
                return false; // unknown modifier
        }

        return TryGetVirtualKey(parts[^1], out vk);
    }

    private static bool TryGetVirtualKey(string name, out uint vk)
    {
        vk = 0;

        if (name.Length == 1)
        {
            var c = name[0];
            if (c is >= 'A' and <= 'Z') { vk = c; return true; }
            if (c is >= 'a' and <= 'z') { vk = char.ToUpperInvariant(c); return true; }
            if (c is >= '0' and <= '9') { vk = c; return true; }
            return false;
        }

        if (name.Length >= 2 && name[0] == 'F' && int.TryParse(name.AsSpan(1), out var fn) && fn is >= 1 and <= 24)
        {
            vk = (uint)(0x70 + fn - 1);
            return true;
        }

        return NamedKeys.TryGetValue(name, out vk);
    }

    // --- Win32 interop ---

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
