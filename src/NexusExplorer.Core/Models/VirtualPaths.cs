namespace NexusExplorer.Core.Models;

/// <summary>
/// Constants for virtual (non-filesystem) locations used by navigation.
/// </summary>
public static class VirtualPaths
{
    public const string ThisPC = "shell:ThisPC";
    public const string Network = "shell:Network";
    public const string RecycleBin = "shell:RecycleBin";

    /// <summary>Prefix for a virtual view that lists all folders assigned a given color.</summary>
    public const string ColorGroupPrefix = "shell:color:";

    /// <summary>
    /// Returns true if the path is a virtual (non-filesystem) location.
    /// </summary>
    public static bool IsVirtual(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns true if the path is a color-group virtual location.</summary>
    public static bool IsColorGroup(string? path)
        => !string.IsNullOrEmpty(path)
           && path.StartsWith(ColorGroupPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds the virtual path for the color-group view of the given color hex.</summary>
    public static string ColorGroup(string colorHex) => ColorGroupPrefix + colorHex;

    /// <summary>Extracts the color hex from a color-group virtual path, or null if not one.</summary>
    public static string? GetColorHex(string? path)
        => IsColorGroup(path) ? path![ColorGroupPrefix.Length..] : null;

    /// <summary>
    /// Returns a human-friendly display name for a virtual path.
    /// </summary>
    public static string GetDisplayName(string path)
    {
        if (IsColorGroup(path))
        {
            // Show the color's name ("Purple") instead of its hex; fall back to the hex
            // for non-preset colors.
            var hex = GetColorHex(path);
            return Abstractions.FolderColorOption.GetPresetName(hex) ?? $"Color {hex}";
        }

        return path switch
        {
            ThisPC => "This PC",
            Network => "Network",
            RecycleBin => "Recycle Bin",
            _ => path
        };
    }
}
