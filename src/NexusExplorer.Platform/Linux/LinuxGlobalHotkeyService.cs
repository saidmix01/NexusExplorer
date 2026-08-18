using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.Linux;

/// <summary>
/// Global hotkey support for Linux. Reserved for a future implementation
/// (e.g. via X11/Wayland global shortcuts).
/// </summary>
public sealed class LinuxGlobalHotkeyService : IGlobalHotkeyService
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
