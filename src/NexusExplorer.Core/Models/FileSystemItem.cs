namespace NexusExplorer.Core.Models;

/// <summary>
/// Represents a file or directory in the file system.
/// </summary>
public sealed class FileSystemItem
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required FileSystemItemType Type { get; init; }
    public long? Size { get; init; }
    public DateTime? LastModified { get; init; }
    public DateTime? Created { get; init; }
    public string? Extension { get; init; }
    public bool IsHidden { get; init; }
    public bool IsReadOnly { get; init; }
    
    // --- Drive info (only for Type == Drive) ---
    public long? TotalSpace { get; init; }
    public long? FreeSpace { get; init; }
    public double UsagePercent => TotalSpace is > 0 ? (double)(TotalSpace.Value - (FreeSpace ?? 0)) / TotalSpace.Value * 100 : 0;

    // --- Grouping ---
    public bool IsGroupHeader { get; init; }
    public string? GroupName { get; init; }

    /// <summary>
    /// Custom folder color hex (e.g. "#4A9FDE") assigned to this folder, or null if none.
    /// Populated by the view model at load time from the folder-color service so the UI can
    /// show a color swatch and sort/group by color without querying the service per item.
    /// </summary>
    public string? FolderColor { get; init; }
}
