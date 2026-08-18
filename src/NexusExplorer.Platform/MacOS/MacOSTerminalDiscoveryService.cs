using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.MacOS;

/// <summary>
/// Detects terminals installed on macOS. Reserved for future implementation —
/// can detect Terminal.app, iTerm2, etc.
/// </summary>
public sealed class MacOSTerminalDiscoveryService : ITerminalDiscoveryService
{
    public IReadOnlyList<TerminalProfile> Discover()
    {
        // TODO: add macOS terminal profiles (e.g. Terminal.app, iTerm2).
        return Array.Empty<TerminalProfile>();
    }
}
