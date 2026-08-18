using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.MacOS;

/// <summary>
/// macOS implementation of ITerminalSessionService.
/// Currently returns NotSupported — full PTY implementation is planned for a future iteration.
/// The abstraction is ready for a proper posix_openpt-based implementation.
/// </summary>
public sealed class MacOSTerminalSessionService : ITerminalSessionService
{
    public bool IsSupported => false;

    public string? GetDefaultShellPath()
    {
        // Prefer user's configured shell
        var shell = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrEmpty(shell) && File.Exists(shell))
            return shell;

        // Fall back to zsh (default on modern macOS)
        if (File.Exists("/bin/zsh")) return "/bin/zsh";
        if (File.Exists("/bin/bash")) return "/bin/bash";

        return null;
    }

    public Task<ITerminalSession> CreateSessionAsync(TerminalSessionOptions options, CancellationToken cancellationToken = default)
    {
        throw new PlatformNotSupportedException(
            "Embedded terminal sessions are not yet supported on macOS. " +
            "A PTY-based implementation is planned for a future release.");
    }
}
