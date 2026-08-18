namespace NexusExplorer.Core.Models;

/// <summary>
/// Represents a segment in the breadcrumb/address bar.
/// </summary>
public sealed class BreadcrumbItem
{
    public required string Name { get; init; }
    public required string Path { get; init; }

    /// <summary>
    /// Whether this is the last (current directory) segment in the breadcrumb trail.
    /// Used for visual hierarchy — the last segment is displayed bold and bright.
    /// </summary>
    public bool IsLast { get; set; }
}
