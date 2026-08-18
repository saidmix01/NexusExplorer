namespace NexusExplorer.Core.Models;

/// <summary>
/// Represents a drive/volume with storage information for display in This PC.
/// </summary>
public sealed class DriveItem
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required DriveCategory Category { get; init; }
    public long TotalSize { get; init; }
    public long FreeSpace { get; init; }
    public long UsedSpace => TotalSize - FreeSpace;
    public double UsagePercent => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;
    public string? VolumeLabel { get; init; }
    public string DriveLetter { get; init; } = string.Empty;
    public bool IsReady { get; init; } = true;
}

public enum DriveCategory
{
    Fixed,
    Removable,
    Network,
    Optical,
    Ram,
    Unknown
}
