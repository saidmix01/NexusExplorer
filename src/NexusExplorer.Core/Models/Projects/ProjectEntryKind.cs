namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// What a <see cref="ProjectEntry"/> points at, so the UI knows how to act when it is opened.
/// </summary>
public enum ProjectEntryKind
{
    /// <summary>A real directory on disk; opening it navigates the file explorer into it.</summary>
    Directory = 0,

    /// <summary>A real file on disk; opening it launches the OS default handler.</summary>
    File,

    /// <summary>A runnable command (e.g. a script). Carries a <see cref="ProjectCommand"/> for Nexus Actions.</summary>
    Command,

    /// <summary>Informational text only (e.g. "Repository detected"); not actionable.</summary>
    Information
}
