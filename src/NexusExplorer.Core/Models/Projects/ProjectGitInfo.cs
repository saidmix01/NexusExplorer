namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// Minimal Git information for a project.
/// </summary>
/// <remarks>
/// For the MVP only <see cref="HasRepository"/> is populated (presence of a <c>.git</c> entry at
/// the project root). The remaining members are intentionally nullable placeholders so a future
/// dedicated Git service can fill them in without changing the model or the UI. They are left
/// <c>null</c> rather than guessed so the UI never shows unreliable data.
/// </remarks>
public sealed class ProjectGitInfo
{
    /// <summary>True when a <c>.git</c> repository was found at the project root.</summary>
    public required bool HasRepository { get; init; }

    /// <summary>Current branch name, or <c>null</c> if not yet resolved.</summary>
    public string? CurrentBranch { get; init; }

    /// <summary>Number of modified (unstaged) files, or <c>null</c> if not yet resolved.</summary>
    public int? ModifiedFileCount { get; init; }

    /// <summary>Number of staged files, or <c>null</c> if not yet resolved.</summary>
    public int? StagedFileCount { get; init; }
}
