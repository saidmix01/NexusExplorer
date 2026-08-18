using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform;

/// <summary>
/// Launches terminal profiles as external processes. Working-directory handling is driven
/// by each profile's <see cref="TerminalWorkingDirectoryMode"/> so callers never need to
/// know terminal-specific launch semantics. Paths are always treated as data, never as code.
/// </summary>
public sealed class TerminalLauncher : ITerminalLauncher
{
    private readonly ILogger<TerminalLauncher> _logger;

    public TerminalLauncher(ILogger<TerminalLauncher> logger)
    {
        _logger = logger;
    }

    public void Launch(TerminalProfile profile, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(profile.ExecutablePath))
            throw new ArgumentException("Terminal profile has no executable path.", nameof(profile));

        var startInfo = new ProcessStartInfo
        {
            FileName = profile.ExecutablePath,
            UseShellExecute = profile.RequiresShellExecute,
        };

        switch (profile.WorkingDirectoryMode)
        {
            case TerminalWorkingDirectoryMode.ProcessWorkingDirectory:
                startInfo.WorkingDirectory = workingDirectory;
                AddArguments(startInfo, profile, profile.Arguments);
                break;

            case TerminalWorkingDirectoryMode.WorkingDirectoryArgument:
                AddArguments(startInfo, profile, profile.Arguments);

                var directory = profile.ConvertToWslPath ? ToWslPath(workingDirectory) : workingDirectory;
                if (!string.IsNullOrWhiteSpace(profile.WorkingDirectoryArgument))
                {
                    if (profile.WorkingDirectoryArgument.EndsWith('='))
                        AddArguments(startInfo, profile, new[] { profile.WorkingDirectoryArgument + directory });
                    else
                        AddArguments(startInfo, profile, new[] { profile.WorkingDirectoryArgument, directory });
                }
                break;

            case TerminalWorkingDirectoryMode.None:
            default:
                AddArguments(startInfo, profile, profile.Arguments);
                break;
        }

        _logger.LogInformation(
            "Launching terminal '{Name}' ({ExecutablePath}) in '{WorkingDirectory}'.",
            profile.Name, profile.ExecutablePath, workingDirectory);

        using var process = Process.Start(startInfo);
    }

    private static void AddArguments(ProcessStartInfo startInfo, TerminalProfile profile, IEnumerable<string> arguments)
    {
        foreach (var arg in arguments)
        {
            if (profile.RequiresShellExecute)
            {
                // ShellExecute requires a single pre-quoted command line (ArgumentList is unsupported).
                startInfo.Arguments = string.IsNullOrEmpty(startInfo.Arguments)
                    ? QuoteWindowsArg(arg)
                    : startInfo.Arguments + " " + QuoteWindowsArg(arg);
            }
            else
            {
                // ArgumentList lets .NET quote/escape each argument safely.
                startInfo.ArgumentList.Add(arg);
            }
        }
    }

    /// <summary>
    /// Quotes a single Windows command-line argument. Windows paths cannot contain a double
    /// quote, but embedded quotes are escaped defensively.
    /// </summary>
    private static string QuoteWindowsArg(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (arg.Any(char.IsWhiteSpace) || arg.Contains('"'))
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        return arg;
    }

    /// <summary>
    /// Converts a Windows path to a WSL path, e.g. C:\Projects\Nexus -> /mnt/c/Projects/Nexus.
    /// </summary>
    private static string ToWslPath(string path)
    {
        var full = Path.GetFullPath(path);

        if (full.Length >= 2 && full[1] == ':')
        {
            var drive = char.ToLowerInvariant(full[0]);
            var remainder = full[2..].Replace('\\', '/').TrimStart('/');
            return $"/mnt/{drive}/{remainder}";
        }

        return full.Replace('\\', '/');
    }
}
