using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.Linux;

/// <summary>
/// Detects terminals installed on Linux. Reserved for future implementation —
/// can detect GNOME Terminal, Konsole, XFCE Terminal, Alacritty, Kitty, etc.
/// </summary>
public sealed class LinuxTerminalDiscoveryService : ITerminalDiscoveryService
{
    public IReadOnlyList<TerminalProfile> Discover()
    {
        // TODO: add Linux terminal profiles (e.g. gnome-terminal, konsole, alacritty, kitty).
        return Array.Empty<TerminalProfile>();
    }
}
