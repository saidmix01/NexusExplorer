namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// A software project technology Nexus can recognize.
/// Extend this enum (and add a matching detector) to support new ecosystems.
/// </summary>
public enum ProjectType
{
    /// <summary>No recognizable project technology was detected.</summary>
    Unknown = 0,
    Node,
    DotNet,
    Rust,
    Python
}
