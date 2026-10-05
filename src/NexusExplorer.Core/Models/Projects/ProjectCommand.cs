namespace NexusExplorer.Core.Models.Projects;

/// <summary>
/// A structured, ready-to-run command exposed by a project (e.g. <c>npm run dev</c>,
/// <c>dotnet build</c>, <c>cargo test</c>).
/// </summary>
/// <remarks>
/// Project Explorer only <b>describes</b> commands; it never executes them. The data here is
/// the contract that Nexus Actions will later consume to run the command. Keeping the executable
/// and arguments separate (rather than a single string) lets the consumer launch processes
/// safely without shell-string parsing.
/// </remarks>
public sealed class ProjectCommand
{
    /// <summary>Human-readable label shown in the UI (e.g. "dev", "build", "dotnet test").</summary>
    public required string Label { get; init; }

    /// <summary>The executable to invoke (e.g. "npm", "dotnet", "cargo", "python").</summary>
    public required string Executable { get; init; }

    /// <summary>
    /// Ordered arguments passed to <see cref="Executable"/> (e.g. ["run", "dev"]).
    /// Kept as discrete tokens so a consumer can start the process without shell parsing.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Working directory the command should run in (usually the project root).</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Where the command came from (fixed tooling vs. a user-defined script).</summary>
    public ProjectCommandSource Source { get; init; } = ProjectCommandSource.Tooling;

    /// <summary>
    /// A display-only representation of the full command line (executable + arguments).
    /// For UI/tooltips only; consumers should launch using <see cref="Executable"/> and
    /// <see cref="Arguments"/> rather than parsing this string.
    /// </summary>
    public string DisplayCommand =>
        Arguments.Count == 0 ? Executable : $"{Executable} {string.Join(' ', Arguments)}";
}
