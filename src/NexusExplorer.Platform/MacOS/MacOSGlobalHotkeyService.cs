using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.MacOS;

/// <summary>
/// Global hotkey support for macOS. Reserved for a future implementation
/// (e.g. via Carbon RegisterEventHotKey or a system accessibility service).
/// </summary>
public sealed class MacOSGlobalHotkeyService : IGlobalHotkeyService
{
    public bool IsSupported => false;

    public event Action<string>? HotkeyTriggered
    {
        add { }
        remove { }
    }

    public bool Register(GlobalHotkey hotkey) => false;

    public void Unregister(string id) { }

    public void UnregisterAll() { }

    public void Dispose() { }
}
