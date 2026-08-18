using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Registers and unregisters system-wide (global) hotkeys.
/// Implementations are platform-specific; the rest of the application only
/// depends on this abstraction.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>True if the current platform supports global hotkeys.</summary>
    bool IsSupported { get; }

    /// <summary>Raised when a registered hotkey is pressed. The argument is the hotkey Id.</summary>
    event Action<string>? HotkeyTriggered;

    /// <summary>
    /// Registers a hotkey. Returns false if the shortcut is already taken by
    /// another application or by Windows.
    /// </summary>
    bool Register(GlobalHotkey hotkey);

    /// <summary>Unregisters the hotkey with the given Id.</summary>
    void Unregister(string id);

    /// <summary>Unregisters all currently registered hotkeys.</summary>
    void UnregisterAll();
}
