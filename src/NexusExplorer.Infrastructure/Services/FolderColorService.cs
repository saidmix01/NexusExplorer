using System.Text.Json;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Infrastructure.Services;

/// <summary>
/// Persists folder color assignments to a JSON file in LocalApplicationData.
/// </summary>
public sealed class FolderColorService : IFolderColorService
{
    private readonly string _filePath;
    private Dictionary<string, string> _colors = new(StringComparer.OrdinalIgnoreCase);

    private static readonly List<FolderColorOption> Presets =
    [
        new() { Name = "Blue", ColorHex = "#4A9FDE" },
        new() { Name = "Red", ColorHex = "#E05555" },
        new() { Name = "Green", ColorHex = "#4CAF50" },
        new() { Name = "Orange", ColorHex = "#FF9800" },
        new() { Name = "Purple", ColorHex = "#9C27B0" },
        new() { Name = "Pink", ColorHex = "#E91E63" },
        new() { Name = "Teal", ColorHex = "#009688" },
        new() { Name = "Yellow", ColorHex = "#FFC107" },
        new() { Name = "Gray", ColorHex = "#78909C" },
    ];

    public event EventHandler<string>? ColorChanged;

    public FolderColorService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexusExplorer");
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, "folder-colors.json");
        Load();
    }

    public string? GetColor(string folderPath)
    {
        return _colors.TryGetValue(folderPath, out var color) ? color : null;
    }

    public void SetColor(string folderPath, string? colorHex)
    {
        if (string.IsNullOrEmpty(colorHex))
            _colors.Remove(folderPath);
        else
            _colors[folderPath] = colorHex;

        Save();
        ColorChanged?.Invoke(this, folderPath);
    }

    public IReadOnlyList<FolderColorOption> GetPresetColors() => Presets;

    public IReadOnlyDictionary<string, string> GetAllColors()
        => new Dictionary<string, string>(_colors, StringComparer.OrdinalIgnoreCase);

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data is not null)
                    _colors = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Ignore load failures — start with empty
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_colors, new JsonSerializerOptions { WriteIndented = true });

            // Atomic write: temp file then replace, so an interrupted save can't corrupt the file.
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(_filePath))
                File.Replace(tempPath, _filePath, null);
            else
                File.Move(tempPath, _filePath);
        }
        catch
        {
            // Ignore save failures
        }
    }
}
