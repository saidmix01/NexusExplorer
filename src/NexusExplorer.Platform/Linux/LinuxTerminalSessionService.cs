using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.Linux;

/// <summary>
/// Linux implementation of ITerminalSessionService.
/// Currently returns NotSupported — full PTY implementation is planned for a future iteration.
/// The abstraction is ready for a proper /dev/ptmx-based implementation.
/// </summary>
public sealed class LinuxTerminalSessionService : ITerminalSessionService
{
    public bool IsSupported => false;

    public string? GetDefaultShellPath()
    {
        // Prefer user's configured shell
        var shell = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrEmpty(shell) && File.Exists(shell))
            return shell;

        // Fall back to common shells
        if (File.Exists("/usr/bin/bash")) return "/usr/bin/bash";
        if (File.Exists("/bin/bash")) return "/bin/bash";
        if (File.Exists("/bin/sh")) return "/bin/sh";

        return null;
    }

    public Task<ITerminalSession> CreateSessionAsync(TerminalSessionOptions options, CancellationToken cancellationToken = default)
    {
        throw new PlatformNotSupportedException(
            "Embedded terminal sessions are not yet supported on Linux. " +
            "A PTY-based implementation is planned for a future release.");
    }
}
