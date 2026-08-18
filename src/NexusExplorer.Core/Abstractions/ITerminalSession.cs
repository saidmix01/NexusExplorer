namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Represents an active terminal session with a running shell process.
/// </summary>
public interface ITerminalSession : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Unique identifier for this session.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The initial working directory this session was started with.
    /// </summary>
    string InitialWorkingDirectory { get; }

    /// <summary>
    /// The last known working directory. May equal InitialWorkingDirectory
    /// if directory change detection is not available.
    /// </summary>
    string CurrentWorkingDirectory { get; }

    /// <summary>
    /// Whether the shell process is still running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// The process ID of the shell, if available.
    /// </summary>
    int? ProcessId { get; }

    /// <summary>
    /// Writes input bytes to the terminal (keyboard input from the user).
    /// </summary>
    Task WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a string as input to the terminal (convenience method, encodes as UTF-8).
    /// </summary>
    Task WriteInputAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resizes the terminal to the specified dimensions.
    /// </summary>
    Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the terminal session, terminating the shell process if still running.
    /// </summary>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fired when output data is received from the terminal (raw bytes including ANSI sequences).
    /// </summary>
    event EventHandler<ReadOnlyMemory<byte>>? OutputReceived;

    /// <summary>
    /// Fired when the shell process exits.
    /// </summary>
    event EventHandler<int>? ProcessExited;
}
