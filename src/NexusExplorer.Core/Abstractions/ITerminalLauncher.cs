using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Launches an external terminal profile in a given working directory.
/// Callers only supply a profile and directory; terminal-specific launch semantics
/// live inside the implementation.
/// </summary>
public interface ITerminalLauncher
{
    void Launch(TerminalProfile profile, string workingDirectory);
}
