using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for creating and managing embedded terminal sessions.
/// Separate from ITerminalService which launches external terminal applications.
/// </summary>
public interface ITerminalSessionService
{
    /// <summary>
    /// Whether the current platform supports embedded terminal sessions.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Gets the path to the default shell executable for this platform.
    /// Returns null if no supported shell is found.
    /// </summary>
    string? GetDefaultShellPath();

    /// <summary>
    /// Creates a new terminal session with the specified options.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown if the platform does not support embedded terminal sessions.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the shell executable cannot be found.
    /// </exception>
    Task<ITerminalSession> CreateSessionAsync(TerminalSessionOptions options, CancellationToken cancellationToken = default);
}
