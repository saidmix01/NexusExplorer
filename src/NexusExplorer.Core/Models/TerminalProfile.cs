namespace NexusExplorer.Core.Models;

/// <summary>
/// The target platform for a terminal profile.
/// </summary>
public enum TerminalPlatform
{
    Windows,
    Linux,
    MacOS,
}

/// <summary>
/// Describes how a terminal's initial working directory is applied on launch.
/// </summary>
public enum TerminalWorkingDirectoryMode
{
    /// <summary>The terminal does not support an explicit working directory.</summary>
    None,

    /// <summary>
    /// Set <see cref="System.Diagnostics.ProcessStartInfo.WorkingDirectory"/> directly.
    /// Used by PowerShell, pwsh, cmd and similar shells that honour the process cwd.
    /// </summary>
    ProcessWorkingDirectory,

    /// <summary>
    /// Pass the directory as a dedicated command-line argument (e.g. Windows Terminal's -d).
    /// </summary>
    WorkingDirectoryArgument,
}

/// <summary>
/// A launchable external terminal detected on the system. Profiles are produced by the
/// platform-specific terminal discovery service and consumed by the terminal launcher.
/// </summary>
public sealed class TerminalProfile
{
    /// <summary>Stable identifier for the profile (e.g. "windows-terminal").</summary>
    public required string Id { get; init; }

    /// <summary>Display name shown in the context menu.</summary>
    public required string Name { get; init; }

    /// <summary>Full path to the terminal executable.</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>Static arguments always passed to the terminal.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The argument switch that receives the working directory (e.g. "-d" for Windows
    /// Terminal, "--cd" for WSL). Only used when <see cref="WorkingDirectoryMode"/> is
    /// <see cref="TerminalWorkingDirectoryMode.WorkingDirectoryArgument"/>.
    /// </summary>
    public string? WorkingDirectoryArgument { get; init; }

    public TerminalWorkingDirectoryMode WorkingDirectoryMode { get; init; } =
        TerminalWorkingDirectoryMode.ProcessWorkingDirectory;

    /// <summary>Converts a Windows path to a WSL (/mnt/...) path before passing it.</summary>
    public bool ConvertToWslPath { get; init; }

    /// <summary>
    /// True when the process must be started with
    /// <see cref="System.Diagnostics.ProcessStartInfo.UseShellExecute"/> (e.g. Windows
    /// Terminal's app execution alias).
    /// </summary>
    public bool RequiresShellExecute { get; init; }

    /// <summary>Icon identifier used by the UI to select a terminal glyph.</summary>
    public string IconKey { get; init; } = "terminal";

    public TerminalPlatform Platform { get; init; }
}
