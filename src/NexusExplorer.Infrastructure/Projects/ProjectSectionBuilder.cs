using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.Infrastructure.Projects;

/// <summary>
/// Builds the ordered, logical <see cref="ProjectSection"/> list from the merged project data.
/// Centralizing this here keeps the Views free of grouping logic and guarantees sections are
/// derived from <see cref="ProjectInfo"/> data rather than hardcoded for any specific project.
/// </summary>
internal static class ProjectSectionBuilder
{
    public static IReadOnlyList<ProjectSection> Build(
        IReadOnlyList<string> projectFiles,
        IReadOnlyList<string> sourceDirectories,
        IReadOnlyList<string> testDirectories,
        IReadOnlyList<string> assetDirectories,
        IReadOnlyList<string> configurationFiles,
        IReadOnlyList<string> documentationFiles,
        IReadOnlyList<ProjectCommand> commands,
        ProjectGitInfo? git)
    {
        var sections = new List<ProjectSection>();

        AddFileSection(sections, ProjectSectionKind.Project, "PROJECT", projectFiles);
        AddPathSection(sections, ProjectSectionKind.Source, "SOURCE", sourceDirectories, ProjectEntryKind.Directory);
        AddPathSection(sections, ProjectSectionKind.Tests, "TESTS", testDirectories, ProjectEntryKind.Directory);
        AddPathSection(sections, ProjectSectionKind.Assets, "ASSETS", assetDirectories, ProjectEntryKind.Directory);
        AddFileSection(sections, ProjectSectionKind.Configuration, "CONFIGURATION", configurationFiles);
        AddFileSection(sections, ProjectSectionKind.Documentation, "DOCUMENTATION", documentationFiles);
        AddScriptsSection(sections, commands);
        AddDevelopmentSection(sections, git);

        return sections;
    }

    private static void AddFileSection(
        List<ProjectSection> sections, ProjectSectionKind kind, string title, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
            return;

        var entries = paths.Select(p => new ProjectEntry
        {
            Name = Path.GetFileName(p),
            Kind = ProjectEntryKind.File,
            Path = p
        }).ToList();

        sections.Add(new ProjectSection { Kind = kind, Title = title, Entries = entries });
    }

    private static void AddPathSection(
        List<ProjectSection> sections, ProjectSectionKind kind, string title,
        IReadOnlyList<string> paths, ProjectEntryKind entryKind)
    {
        if (paths.Count == 0)
            return;

        var entries = paths.Select(p => new ProjectEntry
        {
            Name = Path.GetFileName(p),
            Kind = entryKind,
            Path = p
        }).ToList();

        sections.Add(new ProjectSection { Kind = kind, Title = title, Entries = entries });
    }

    private static void AddScriptsSection(List<ProjectSection> sections, IReadOnlyList<ProjectCommand> commands)
    {
        if (commands.Count == 0)
            return;

        var entries = commands.Select(c => new ProjectEntry
        {
            Name = c.Label,
            Kind = ProjectEntryKind.Command,
            Command = c
        }).ToList();

        sections.Add(new ProjectSection
        {
            Kind = ProjectSectionKind.Scripts,
            Title = "SCRIPTS",
            Entries = entries
        });
    }

    private static void AddDevelopmentSection(List<ProjectSection> sections, ProjectGitInfo? git)
    {
        if (git is not { HasRepository: true })
            return;

        var entries = new List<ProjectEntry>
        {
            new() { Name = "Repository detected", Kind = ProjectEntryKind.Information }
        };

        sections.Add(new ProjectSection
        {
            Kind = ProjectSectionKind.Development,
            Title = "GIT",
            Entries = entries
        });
    }
}
