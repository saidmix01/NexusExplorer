using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Detects WinRAR installation and provides methods to launch WinRAR operations.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WinRarService
{
    private static string? _winRarPath;
    private static bool _checked;

    /// <summary>
    /// Returns the path to WinRAR.exe if installed, or null.
    /// </summary>
    public static string? GetWinRarPath()
    {
        if (_checked) return _winRarPath;
        _checked = true;

        // Check registry for WinRAR installation
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WinRAR");
            if (key is not null)
            {
                var path = key.GetValue("exe64") as string ?? key.GetValue("exe32") as string;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    _winRarPath = path;
                    return _winRarPath;
                }
            }
        }
        catch { }

        // Check common paths
        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinRAR", "WinRAR.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinRAR", "WinRAR.exe"),
            @"C:\Program Files\WinRAR\WinRAR.exe",
            @"C:\Program Files (x86)\WinRAR\WinRAR.exe"
        };

        foreach (var p in commonPaths)
        {
            if (File.Exists(p))
            {
                _winRarPath = p;
                return _winRarPath;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether WinRAR is available on this system.
    /// </summary>
    public static bool IsAvailable => GetWinRarPath() is not null;

    /// <summary>
    /// Opens an archive file in WinRAR.
    /// </summary>
    public static void OpenInWinRar(string archivePath)
    {
        var exe = GetWinRarPath();
        if (exe is null) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"\"{archivePath}\"",
            UseShellExecute = false
        });
    }

    /// <summary>
    /// Extracts an archive to the same directory using WinRAR.
    /// </summary>
    public static void ExtractHere(string archivePath)
    {
        var exe = GetWinRarPath();
        if (exe is null) return;
        var dir = Path.GetDirectoryName(archivePath) ?? ".";
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            // WinRAR wants a trailing backslash on the destination. A single backslash before
            // the closing quote (\") is parsed as an escaped quote by the CRT, corrupting the
            // argument — double it so it becomes a literal backslash followed by the quote.
            Arguments = $"x -ibck -o+ \"{archivePath}\" \"{dir}\\\\\"",
            UseShellExecute = false
        });
    }

    /// <summary>
    /// Extracts an archive to a subfolder named after the archive.
    /// </summary>
    public static void ExtractToFolder(string archivePath)
    {
        var exe = GetWinRarPath();
        if (exe is null) return;
        var dir = Path.GetDirectoryName(archivePath) ?? ".";
        var folderName = Path.GetFileNameWithoutExtension(archivePath);
        var destDir = Path.Combine(dir, folderName);
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"x -ibck -o+ \"{archivePath}\" \"{destDir}\\\\\"",
            UseShellExecute = false
        });
    }

    /// <summary>
    /// Opens the "Add to archive" dialog in WinRAR for the given files/folders.
    /// </summary>
    public static void AddToArchive(IReadOnlyList<string> paths)
    {
        var exe = GetWinRarPath();
        if (exe is null || paths.Count == 0) return;

        // WinRAR supports multiple files via space-separated quoted paths
        var args = "a -ibck -- \"" + GetDefaultArchiveName(paths) + "\" " +
                   string.Join(" ", paths.Select(p => $"\"{p}\""));
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false
        });
    }

    /// <summary>
    /// Opens WinRAR's "Add to archive" dialog (interactive) for the given paths.
    /// </summary>
    public static void AddToArchiveDialog(IReadOnlyList<string> paths)
    {
        var exe = GetWinRarPath();
        if (exe is null || paths.Count == 0) return;

        // No -ibck flag = interactive dialog
        var pathArgs = string.Join(" ", paths.Select(p => $"\"{p}\""));
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"a -- {pathArgs}",
            UseShellExecute = false
        });
    }

    private static string GetDefaultArchiveName(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1)
        {
            var name = Path.GetFileNameWithoutExtension(paths[0]);
            var dir = Path.GetDirectoryName(paths[0]) ?? ".";
            return Path.Combine(dir, name + ".rar");
        }

        // Multiple items: use parent folder name
        var parentDir = Path.GetDirectoryName(paths[0]) ?? ".";
        var parentName = Path.GetFileName(parentDir);
        return Path.Combine(parentDir, parentName + ".rar");
    }
}
