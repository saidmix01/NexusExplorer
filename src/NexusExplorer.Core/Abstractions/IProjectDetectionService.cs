using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Detects the project (if any) at a folder by running all registered <see cref="IProjectDetector"/>s
/// against a single, fast, root-level scan and assembling a <see cref="ProjectInfo"/>.
/// </summary>
/// <remarks>
/// This is the shared entry point for Developer Mode components (Project Explorer, Nexus Actions,
/// Terminal, Git): detection is performed once here and reused, never duplicated per component.
/// Implementations must be non-blocking and never execute project commands during detection.
/// </remarks>
public interface IProjectDetectionService
{
    /// <summary>
    /// Scans <paramref name="folderPath"/> at the root level and returns a <see cref="ProjectInfo"/>
    /// when a known project is detected, or <c>null</c> when the folder is not a recognized project.
    /// </summary>
    Task<ProjectInfo?> DetectAsync(string folderPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears any cached detection for the given folder (e.g. after a manifest file changed).
    /// Pass <c>null</c> to clear the entire cache.
    /// </summary>
    void Invalidate(string? folderPath = null);
}
