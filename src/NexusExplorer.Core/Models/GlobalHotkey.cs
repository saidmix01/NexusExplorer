namespace NexusExplorer.Core.Models;

/// <summary>
/// A global hotkey (works system-wide, not just when the app has focus).
/// The shortcut is stored as a canonical display string such as "Ctrl+Alt+E";
/// platform implementations parse it into their native key/modifier codes.
/// </summary>
public sealed class GlobalHotkey
{
    /// <summary>Stable identifier, e.g. "open-nexus".</summary>
    public required string Id { get; init; }

    /// <summary>Whether the hotkey is currently enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Canonical shortcut string, e.g. "Ctrl+Alt+E".</summary>
    public string Shortcut { get; set; } = "Ctrl+Alt+E";
}
