namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// Where a <see cref="ProjectCommand"/> originated from, so Nexus Actions can
/// group or label commands appropriately without re-inspecting the project.
/// </summary>
public enum ProjectCommandSource
{
    /// <summary>A fixed command for the detected tooling (e.g. <c>dotnet build</c>, <c>cargo run</c>).</summary>
    Tooling = 0,

    /// <summary>A user-defined script read from project metadata (e.g. a <c>package.json</c> script).</summary>
    Script
}
