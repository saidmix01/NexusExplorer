using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Detects the terminals installed on the current platform and returns launchable profiles.
/// Implementations are platform-specific so each OS can provide its own set of terminals.
/// </summary>
public interface ITerminalDiscoveryService
{
    IReadOnlyList<TerminalProfile> Discover();
}
