using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Platform.Windows.ConPty;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Windows implementation of ITerminalSessionService using ConPTY.
/// Detects and uses PowerShell 7 (pwsh) or Windows PowerShell as the default shell.
/// </summary>
public sealed class WindowsTerminalSessionService : ITerminalSessionService
{
    public bool IsSupported => true;

    public string? GetDefaultShellPath()
    {
        // Prefer PowerShell 7 (pwsh.exe)
        var pwsh = FindExecutable("pwsh.exe");
        if (pwsh is not null)
            return pwsh;

        // Fall back to Windows PowerShell
        var windowsPowerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"WindowsPowerShell\v1.0\powershell.exe");

        if (File.Exists(windowsPowerShell))
            return windowsPowerShell;

        return null;
    }

    public Task<ITerminalSession> CreateSessionAsync(TerminalSessionOptions options, CancellationToken cancellationToken = default)
    {
        var shellPath = options.ShellPath ?? GetDefaultShellPath();

        if (string.IsNullOrEmpty(shellPath))
            throw new InvalidOperationException("No supported PowerShell executable found on this system.");

        if (!Directory.Exists(options.WorkingDirectory))
            throw new InvalidOperationException($"Working directory does not exist: {options.WorkingDirectory}");

        var columns = (short)Math.Clamp(options.Columns, 1, 500);
        var rows = (short)Math.Clamp(options.Rows, 1, 200);

        // Build command line — use -NoLogo for cleaner startup
        var commandLine = $"\"{shellPath}\" -NoLogo";

        var process = ConPtyProcess.Start(commandLine, options.WorkingDirectory, columns, rows);
        var session = new ConPtyTerminalSession(process, options.WorkingDirectory);

        return Task.FromResult<ITerminalSession>(session);
    }

    private static string? FindExecutable(string fileName)
    {
        // Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        // Check common install location for pwsh
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pwshPath = Path.Combine(programFiles, "PowerShell");
        if (Directory.Exists(pwshPath))
        {
            // Find the latest version directory
            var versions = Directory.GetDirectories(pwshPath)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (versions is not null)
            {
                var exe = Path.Combine(versions, fileName);
                if (File.Exists(exe))
                    return exe;
            }
        }

        return null;
    }
}
