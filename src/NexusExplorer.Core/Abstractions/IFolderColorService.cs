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
}
