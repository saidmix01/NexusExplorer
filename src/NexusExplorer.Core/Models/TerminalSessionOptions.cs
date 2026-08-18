namespace NexusExplorer.Core.Models;

/// <summary>
/// Options for creating a new terminal session.
/// </summary>
public sealed class TerminalSessionOptions
{
    /// <summary>
    /// The initial working directory for the terminal session.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// The shell executable to launch. If null, the platform default is used.
    /// </summary>
    public string? ShellPath { get; init; }

    /// <summary>
    /// Initial terminal width in columns.
    /// </summary>
    public int Columns { get; init; } = 120;

    /// <summary>
    /// Initial terminal height in rows.
    /// </summary>
    public int Rows { get; init; } = 30;
}
