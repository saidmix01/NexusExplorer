using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Detects whether a folder is a project of one specific technology (Node, .NET, Rust, Python…).
/// </summary>
/// <remarks>
/// This is the extension point: supporting a new ecosystem means adding a new implementation,
/// not editing the UI or a central switch. Detectors are pure and synchronous — they work only
/// from the pre-captured <see cref="ProjectProbeContext"/> and must not recurse the file system.
/// </remarks>
public interface IProjectDetector
{
    /// <summary>The technology this detector recognizes.</summary>
    ProjectType Type { get; }

    /// <summary>
    /// Inspects the root-level probe and returns a <see cref="ProjectDetection"/> if the folder
    /// matches this technology, or <c>null</c> otherwise.
    /// </summary>
    ProjectDetection? Detect(ProjectProbeContext context);
}
