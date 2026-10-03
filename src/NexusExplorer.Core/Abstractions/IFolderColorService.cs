namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for managing custom folder colors.
/// Persists color assignments per folder path.
/// </summary>
public interface IFolderColorService
{
    /// <summary>
    /// Gets the assigned color hex string for a folder path, or null if no custom color is set.
    /// </summary>
    string? GetColor(string folderPath);

    /// <summary>
    /// Sets a custom color for a folder path. Pass null to remove the custom color.
    /// </summary>
    void SetColor(string folderPath, string? colorHex);

    /// <summary>
    /// Gets all preset color options available for folder coloring.
    /// </summary>
    IReadOnlyList<FolderColorOption> GetPresetColors();

    /// <summary>
    /// Gets a snapshot of all folder-to-color assignments (folder path -> color hex).
    /// </summary>
    IReadOnlyDictionary<string, string> GetAllColors();

    /// <summary>
    /// Fired when a folder color changes (path that changed is in EventArgs).
    /// </summary>
    event EventHandler<string>? ColorChanged;
}

/// <summary>
/// Represents a preset folder color choice.
/// </summary>
public sealed class FolderColorOption
{
    public required string Name { get; init; }
    public required string ColorHex { get; init; }

    /// <summary>
    /// The built-in preset colors. Single source of truth shared by the folder color service
    /// and by display-name resolution (e.g. color-group tab titles).
    /// </summary>
    public static IReadOnlyList<FolderColorOption> Presets { get; } =
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

    /// <summary>Returns the preset name for a color hex, or null if it isn't a preset.</summary>
    public static string? GetPresetName(string? colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return null;
        foreach (var preset in Presets)
        {
            if (string.Equals(preset.ColorHex, colorHex, StringComparison.OrdinalIgnoreCase))
                return preset.Name;
        }
        return null;
    }
}
