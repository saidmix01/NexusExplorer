using System.Diagnostics;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform;

/// <summary>
/// Discovers installed code editors by checking well-known install locations,
/// scanning common install directories for their executables, and probing PATH.
/// </summary>
public sealed class EditorService : IEditorService
{
    private sealed record EditorCandidate(
        string Name,
        string Exe,
        string Command,
        string[] SpecificPaths);

    private static readonly EditorCandidate[] Candidates =
    [
        new("Visual Studio Code", "Code.exe", "code",
            [@"%LocalAppData%\Programs\Microsoft VS Code\Code.exe", @"%ProgramFiles%\Microsoft VS Code\Code.exe", @"%ProgramFiles(x86)%\Microsoft VS Code\Code.exe"]),
        new("Visual Studio Code Insiders", "Code - Insiders.exe", "code-insiders",
            [@"%LocalAppData%\Programs\Microsoft VS Code Insiders\Code - Insiders.exe"]),
        new("Cursor", "Cursor.exe", "cursor",
            [@"%LocalAppData%\Programs\Cursor\Cursor.exe", @"%LocalAppData%\Programs\cursor\Cursor.exe"]),
        new("Trae", "Trae.exe", "trae",
            [@"%LocalAppData%\Programs\Trae\Trae.exe", @"%LocalAppData%\Programs\Trae CN\Trae.exe"]),
        new("Kiro", "Kiro.exe", "kiro",
            [@"%LocalAppData%\Programs\Kiro\Kiro.exe", @"%LocalAppData%\Programs\kiro\Kiro.exe", @"%LocalAppData%\Kiro\Kiro.exe"]),
        new("Windsurf", "Windsurf.exe", "windsurf",
            [@"%LocalAppData%\Programs\Windsurf\Windsurf.exe"]),
        new("Zed", "zed.exe", "zed",
            [@"%LocalAppData%\Programs\Zed\zed.exe"]),
        new("VSCodium", "VSCodium.exe", "codium",
            [@"%LocalAppData%\Programs\VSCodium\VSCodium.exe", @"%ProgramFiles%\VSCodium\VSCodium.exe"]),
        new("Sublime Text", "sublime_text.exe", "subl",
            [@"%ProgramFiles%\Sublime Text\sublime_text.exe", @"%ProgramFiles(x86)%\Sublime Text\sublime_text.exe"]),
        new("Notepad++", "notepad++.exe", "notepad++",
            [@"%ProgramFiles%\Notepad++\notepad++.exe", @"%ProgramFiles(x86)%\Notepad++\notepad++.exe"]),
        new("Atom", "atom.exe", "atom",
            [@"%LocalAppData%\atom\atom.exe", @"%ProgramFiles%\Atom\atom.exe"]),
        new("Brackets", "Brackets.exe", "brackets",
            [@"%ProgramFiles(x86)%\Brackets\Brackets.exe", @"%ProgramFiles%\Brackets\Brackets.exe"]),
    ];

    public IReadOnlyList<EditorInfo> GetInstalledEditors()
    {
        var editors = new List<EditorInfo>();

        foreach (var candidate in Candidates)
        {
            var path = FindInSpecificPaths(candidate.SpecificPaths)
                       ?? FindInCommonInstallDirs(candidate.Exe)
                       ?? FindOnPath(candidate.Command);

            if (!string.IsNullOrWhiteSpace(path))
                editors.Add(new EditorInfo { Name = candidate.Name, ExecutablePath = path });
        }

        return editors;
    }

    public void Open(EditorInfo editor, string folderPath)
    {
        if (string.IsNullOrWhiteSpace(editor.ExecutablePath) || string.IsNullOrWhiteSpace(folderPath))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = editor.ExecutablePath,
            Arguments = $"\"{folderPath}\"",
            UseShellExecute = false
        });
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

    private static string? FindInCommonInstallDirs(string exe)
    {
        if (!OperatingSystem.IsWindows()) return null;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var roots = new[]
        {
            Path.Combine(localAppData, "Programs"),
            localAppData
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var found = FindFileRecursive(root, exe, 3);
            if (found is not null) return found;
        }

        return null;
    }

    private static string? FindFileRecursive(string directory, string fileName, int depth)
    {
        if (depth < 0 || string.IsNullOrWhiteSpace(directory)) return null;

        try
        {
            if (!Directory.Exists(directory)) return null;

            var direct = Path.Combine(directory, fileName);
            if (File.Exists(direct)) return direct;

            foreach (var sub in Directory.EnumerateDirectories(directory))
            {
                var found = FindFileRecursive(sub, fileName, depth - 1);
                if (found is not null) return found;
            }
        }
        catch
        {
            // Skip inaccessible directories.
        }

        return null;
    }

    private static string? FindOnPath(string command)
    {
        var fileName = OperatingSystem.IsWindows() && !command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
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
}
