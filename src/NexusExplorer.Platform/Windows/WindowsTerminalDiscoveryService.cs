using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Detects external terminals installed on Windows and returns launchable profiles.
/// Only terminals that are actually present on the system are returned.
/// </summary>
public sealed class WindowsTerminalDiscoveryService : ITerminalDiscoveryService
{
    private readonly ILogger<WindowsTerminalDiscoveryService> _logger;

    public WindowsTerminalDiscoveryService(ILogger<WindowsTerminalDiscoveryService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<TerminalProfile> Discover()
    {
        _logger.LogDebug("Searching for installed terminals.");

        var profiles = new List<TerminalProfile>();

        AddWindowsTerminal(profiles);
        AddPowerShell(profiles);
        AddPowerShell7(profiles);
        AddCommandPrompt(profiles);
        AddGitBash(profiles);
        AddWsl(profiles);

        _logger.LogInformation("Found {Count} installed terminal(s).", profiles.Count);
        return profiles;
    }

    private void AddWindowsTerminal(List<TerminalProfile> profiles)
    {
        var wt = FindInSpecificPaths(new[] { @"%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe" })
                 ?? FindOnPath("wt");
        if (wt is null || !File.Exists(wt)) return;

        profiles.Add(new TerminalProfile
        {
            Id = "windows-terminal",
            Name = "Windows Terminal",
            ExecutablePath = wt,
            WorkingDirectoryMode = TerminalWorkingDirectoryMode.WorkingDirectoryArgument,
            WorkingDirectoryArgument = "-d",
            RequiresShellExecute = true,
            IconKey = "windows-terminal",
            Platform = TerminalPlatform.Windows,
        });
        _logger.LogInformation("Found Windows Terminal.");
    }

    private void AddPowerShell(List<TerminalProfile> profiles)
    {
        var ps = FindInSpecificPaths(new[] { @"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" })
                 ?? FindOnPath("powershell");
        if (ps is null || !File.Exists(ps)) return;

        profiles.Add(new TerminalProfile
        {
            Id = "powershell",
            Name = "Windows PowerShell",
            ExecutablePath = ps,
            IconKey = "powershell",
            Platform = TerminalPlatform.Windows,
        });
        _logger.LogInformation("Found Windows PowerShell.");
    }

    private void AddPowerShell7(List<TerminalProfile> profiles)
    {
        var pwsh = FindInSpecificPaths(new[] { @"%ProgramFiles%\PowerShell\7\pwsh.exe" })
                   ?? FindOnPath("pwsh");
        if (pwsh is null || !File.Exists(pwsh)) return;

        profiles.Add(new TerminalProfile
        {
            Id = "powershell-7",
            Name = "PowerShell 7",
            ExecutablePath = pwsh,
            IconKey = "powershell",
            Platform = TerminalPlatform.Windows,
        });
        _logger.LogInformation("Found PowerShell 7.");
    }

    private void AddCommandPrompt(List<TerminalProfile> profiles)
    {
        var cmd = FindInSpecificPaths(new[] { @"%SystemRoot%\System32\cmd.exe" })
                  ?? FindOnPath("cmd");
        if (cmd is null || !File.Exists(cmd)) return;

        profiles.Add(new TerminalProfile
        {
            Id = "cmd",
            Name = "Command Prompt",
            ExecutablePath = cmd,
            IconKey = "cmd",
            Platform = TerminalPlatform.Windows,
        });
        _logger.LogInformation("Found Command Prompt.");
    }

    private void AddGitBash(List<TerminalProfile> profiles)
    {
        var bash = FindGitBash();
        if (bash is null) return;

        profiles.Add(new TerminalProfile
        {
            Id = "git-bash",
            Name = "Git Bash",
            ExecutablePath = bash,
            WorkingDirectoryMode = TerminalWorkingDirectoryMode.WorkingDirectoryArgument,
            WorkingDirectoryArgument = "--cd=",
            IconKey = "git-bash",
            Platform = TerminalPlatform.Windows,
        });
        _logger.LogInformation("Found Git Bash.");
    }

    private void AddWsl(List<TerminalProfile> profiles)
    {
        var wsl = FindInSpecificPaths(new[] { @"%SystemRoot%\System32\wsl.exe" })
                  ?? FindOnPath("wsl");
        if (wsl is null || !File.Exists(wsl)) return;

        // wsl.exe ships with Windows even when no distribution is installed, so verify
        // that at least one distribution is actually available before advertising it.
        if (!IsWslAvailable(wsl)) return;

        profiles.Add(new TerminalProfile
        {
            Id = "wsl",
            Name = "WSL",
            ExecutablePath = wsl,
            WorkingDirectoryMode = TerminalWorkingDirectoryMode.WorkingDirectoryArgument,
            WorkingDirectoryArgument = "--cd",
            ConvertToWslPath = true,
            IconKey = "wsl",
            Platform = TerminalPlatform.Windows,
        });
        _logger.LogInformation("Found WSL.");
    }

    private static string? FindGitBash()
    {
        // 1. Git for Windows registry install path (most reliable for custom installs).
        var installPath = GetGitInstallPath();
        if (installPath is not null)
        {
            var exe = FindGitBashInRoot(installPath);
            if (exe is not null) return exe;
        }

        // 2. Derive the install root from git.exe on PATH.
        var git = FindOnPath("git");
        if (git is not null)
        {
            var root = GetGitRoot(git);
            if (root is not null)
            {
                var exe = FindGitBashInRoot(root);
                if (exe is not null) return exe;
            }
        }

        // 3. Standard install locations.
        foreach (var candidate in new[]
        {
            @"%ProgramFiles%\Git",
            @"%ProgramFiles(x86)%\Git",
            @"%LocalAppData%\Programs\Git",
        })
        {
            var expanded = Environment.ExpandEnvironmentVariables(candidate);
            var exe = FindGitBashInRoot(expanded);
            if (exe is not null) return exe;
        }

        return null;
    }

    private static string? FindGitBashInRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return null;

        foreach (var relative in new[] { "git-bash.exe", @"usr\bin\bash.exe", @"bin\bash.exe" })
        {
            var full = Path.Combine(root, relative);
            if (File.Exists(full)) return full;
        }

        return null;
    }

    private static string? GetGitRoot(string gitExe)
    {
        // git.exe is typically at <root>\cmd\git.exe or <root>\bin\git.exe.
        var dir = Path.GetDirectoryName(gitExe);
        if (dir is null) return null;

        var name = Path.GetFileName(dir);
        return name.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("bin", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(dir)
            : dir;
    }

    private static string? GetGitInstallPath()
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GitForWindows");
            var path = key?.GetValue("InstallPath") as string;
            if (!string.IsNullOrWhiteSpace(path)) return path;

            using var userKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\GitForWindows");
            return userKey?.GetValue("InstallPath") as string;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsWslAvailable(string wslExe)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = wslExe,
                    Arguments = "--list --quiet",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
            };

            if (!process.Start()) return false;

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                return false;
            }

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    private static string? FindOnPath(string command)
    {
        var fileName = !command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? command + ".exe"
            : command;

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var fullPath = Path.Combine(dir.Trim(), fileName);
                if (File.Exists(fullPath)) return fullPath;
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }

    private static string? FindInSpecificPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            if (File.Exists(expanded)) return expanded;
        }

        return null;
    }
}
