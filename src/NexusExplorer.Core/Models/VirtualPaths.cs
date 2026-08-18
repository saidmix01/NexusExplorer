namespace NexusExplorer.Core.Models;

/// <summary>
/// Constants for virtual (non-filesystem) locations used by navigation.
/// </summary>
public static class VirtualPaths
{
    public const string ThisPC = "shell:ThisPC";
    public const string Network = "shell:Network";

    /// <summary>
    /// Returns true if the path is a virtual (non-filesystem) location.
    /// </summary>
    public static bool IsVirtual(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a human-friendly display name for a virtual path.
    /// </summary>
    public static string GetDisplayName(string path)
    {
        return path switch
        {
            ThisPC => "This PC",
            Network => "Network",
            _ => path
        };
    }
}
