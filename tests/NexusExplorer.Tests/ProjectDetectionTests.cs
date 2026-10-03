using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models.Projects;
using NexusExplorer.Infrastructure.Projects;
using NexusExplorer.Infrastructure.Projects.Detectors;

namespace NexusExplorer.Tests;

/// <summary>
/// Detection tests. Each test builds a throwaway temp folder with the relevant marker files so it
/// never depends on real team projects, and cleans up in a finally block (matching the existing
/// file-system test style).
/// </summary>
public class ProjectDetectionTests
{
    private static IProjectDetectionService CreateService() =>
        new ProjectDetectionService(
        [
            new NodeProjectDetector(),
            new DotNetProjectDetector(),
            new RustProjectDetector(),
            new PythonProjectDetector()
        ]);

    /// <summary>Creates a unique temp directory and returns a disposable that deletes it recursively.</summary>
    private static TempDir NewTempDir() => new();

    [Fact]
    public async Task Node_PackageJson_IsDetected()
    {
        using var dir = NewTempDir();
        dir.WriteFile("package.json", """{ "name": "copyfy", "scripts": { "dev": "vite", "build": "vite build" } }""");
        dir.CreateDir("src");
        dir.CreateDir("public");
        dir.WriteFile("README.md", "# Copyfy");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        Assert.Equal(ProjectType.Node, info!.PrimaryType);
        Assert.Equal("copyfy", info.Name);
        Assert.Contains(info.SourceDirectories, p => p.EndsWith("src"));
        Assert.Contains(info.AssetDirectories, p => p.EndsWith("public"));
        Assert.Contains(info.DocumentationFiles, p => p.EndsWith("README.md"));
    }

    [Fact]
    public async Task DotNet_Slnx_IsDetected()
    {
        using var dir = NewTempDir();
        dir.WriteFile("NexusExplorer.slnx", "<Solution />");
        dir.CreateDir("src");
        dir.CreateDir("tests");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        Assert.Equal(ProjectType.DotNet, info!.PrimaryType);
        Assert.Equal("NexusExplorer", info.Name);
        Assert.Contains(info.TestDirectories, p => p.EndsWith("tests"));
    }

    [Theory]
    [InlineData("app.csproj")]
    [InlineData("app.fsproj")]
    [InlineData("app.vbproj")]
    [InlineData("app.sln")]
    public async Task DotNet_ProjectAndSolutionFiles_AreDetected(string manifest)
    {
        using var dir = NewTempDir();
        dir.WriteFile(manifest, "<Project />");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        Assert.Equal(ProjectType.DotNet, info!.PrimaryType);
    }

    [Fact]
    public async Task Rust_CargoToml_IsDetected()
    {
        using var dir = NewTempDir();
        dir.WriteFile("Cargo.toml", "[package]\nname = \"demo\"");
        dir.CreateDir("src");
        dir.CreateDir("tests");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        Assert.Equal(ProjectType.Rust, info!.PrimaryType);
        Assert.Contains(info.Commands, c => c.Executable == "cargo" && c.Arguments.Contains("build"));
    }

    [Theory]
    [InlineData("pyproject.toml")]
    [InlineData("requirements.txt")]
    [InlineData("setup.py")]
    public async Task Python_Markers_AreDetected(string manifest)
    {
        using var dir = NewTempDir();
        dir.WriteFile(manifest, "");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        Assert.Equal(ProjectType.Python, info!.PrimaryType);
    }

    [Fact]
    public async Task Python_RequirementsFile_ExposesPipInstallCommand()
    {
        using var dir = NewTempDir();
        dir.WriteFile("requirements.txt", "pytest\n");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        Assert.Contains(info!.Commands,
            c => c.Executable == "pip" && c.Arguments.Contains("-r") && c.Arguments.Contains("requirements.txt"));
    }

    [Fact]
    public async Task NonProjectFolder_ReturnsNull()
    {
        using var dir = NewTempDir();
        dir.WriteFile("notes.txt", "just some files");
        dir.CreateDir("photos");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.Null(info);
    }

    [Fact]
    public async Task MultipleIndicators_DetectsAllAndPicksDeterministicPrimary()
    {
        using var dir = NewTempDir();
        // Both a Node and a .NET marker present. Priority is DotNet > Node, deterministically.
        dir.WriteFile("package.json", """{ "name": "mixed" }""");
        dir.WriteFile("mixed.csproj", "<Project />");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        Assert.Equal(ProjectType.DotNet, info!.PrimaryType);
        Assert.Contains(ProjectType.DotNet, info.DetectedTechnologies);
        Assert.Contains(ProjectType.Node, info.DetectedTechnologies);
        Assert.Equal(2, info.DetectedTechnologies.Count);
    }

    [Fact]
    public async Task ValidPackageJson_ScriptsBecomeCommands()
    {
        using var dir = NewTempDir();
        dir.WriteFile("package.json",
            """{ "name": "x", "scripts": { "dev": "vite", "build": "vite build", "test": "vitest" } }""");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        // Scripts surface as npm commands; "test" uses the lifecycle shorthand (no "run").
        Assert.Contains(info!.Commands, c => c.Label == "dev" && c.Arguments.SequenceEqual(new[] { "run", "dev" }));
        Assert.Contains(info.Commands, c => c.Label == "build" && c.Arguments.SequenceEqual(new[] { "run", "build" }));
        Assert.Contains(info.Commands, c => c.Label == "test" && c.Arguments.SequenceEqual(new[] { "test" }));
        Assert.All(info.Commands, c => Assert.Equal(dir.Path, c.WorkingDirectory));
    }

    [Fact]
    public async Task InvalidPackageJson_StillDetectsButHasNoScripts()
    {
        using var dir = NewTempDir();
        dir.WriteFile("package.json", "{ this is not valid json ");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        Assert.Equal(ProjectType.Node, info!.PrimaryType);
        // Name falls back to the folder name; only the fixed "install" tooling command remains.
        Assert.DoesNotContain(info.Commands, c => c.Source == ProjectCommandSource.Script);
        Assert.Contains(info.Commands, c => c.Executable == "npm" && c.Arguments.SequenceEqual(new[] { "install" }));
    }

    [Fact]
    public async Task MissingPackageJson_NodeDetectorDoesNotMatch()
    {
        using var dir = NewTempDir();
        dir.CreateDir("src"); // src alone is not a Node project

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.Null(info);
    }

    [Fact]
    public async Task IgnoredDirectories_DoNotDisturbDetection()
    {
        using var dir = NewTempDir();
        dir.WriteFile("package.json", """{ "name": "big" }""");
        // Noise folders that must be ignored and never recursed into.
        dir.CreateDir("node_modules");
        dir.CreateDir("bin");
        dir.CreateDir("obj");
        dir.CreateDir(".git");
        dir.CreateDir("target");
        dir.CreateDir(".venv");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        Assert.Equal(ProjectType.Node, info!.PrimaryType);
        // .git presence is surfaced as a Git repository.
        Assert.NotNull(info.Git);
        Assert.True(info.Git!.HasRepository);
    }

    [Fact]
    public async Task LargeFolder_RootOnlyScan_StaysFast()
    {
        using var dir = NewTempDir();
        dir.WriteFile("Cargo.toml", "[package]\nname = \"big\"");

        // Create many top-level files plus a deep nested tree. Detection must only read the root
        // level, so this should complete quickly regardless of nested depth.
        for (var i = 0; i < 500; i++)
            dir.WriteFile($"file_{i}.txt", "x");

        var deep = dir.Path;
        for (var i = 0; i < 20; i++)
        {
            deep = Path.Combine(deep, $"level_{i}");
            Directory.CreateDirectory(deep);
            File.WriteAllText(Path.Combine(deep, "data.bin"), "x");
        }

        var start = DateTime.UtcNow;
        var info = await CreateService().DetectAsync(dir.Path);
        var elapsed = DateTime.UtcNow - start;

        Assert.NotNull(info);
        Assert.Equal(ProjectType.Rust, info!.PrimaryType);
        // Generous bound; a recursive scan of the nested tree would blow past this.
        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"Detection took too long: {elapsed}");
    }

    [Fact]
    public async Task Sections_AreBuiltFromProjectData()
    {
        using var dir = NewTempDir();
        dir.WriteFile("package.json", """{ "name": "copyfy", "scripts": { "dev": "vite" } }""");
        dir.CreateDir("src");
        dir.CreateDir("tests");
        dir.CreateDir("public");
        dir.WriteFile("README.md", "# Copyfy");
        dir.WriteFile("tsconfig.json", "{}");

        var info = await CreateService().DetectAsync(dir.Path);

        Assert.NotNull(info);
        var kinds = info!.Sections.Select(s => s.Kind).ToList();
        Assert.Contains(ProjectSectionKind.Project, kinds);
        Assert.Contains(ProjectSectionKind.Source, kinds);
        Assert.Contains(ProjectSectionKind.Tests, kinds);
        Assert.Contains(ProjectSectionKind.Assets, kinds);
        Assert.Contains(ProjectSectionKind.Configuration, kinds);
        Assert.Contains(ProjectSectionKind.Documentation, kinds);
        Assert.Contains(ProjectSectionKind.Scripts, kinds);

        // A source entry maps back to the real folder path (file-mapping requirement).
        var source = info.Sections.First(s => s.Kind == ProjectSectionKind.Source).Entries[0];
        Assert.Equal(ProjectEntryKind.Directory, source.Kind);
        Assert.Equal(Path.Combine(dir.Path, "src"), source.Path);
    }

    [Fact]
    public async Task Cache_ReturnsSameInstanceUntilInvalidated()
    {
        using var dir = NewTempDir();
        dir.WriteFile("Cargo.toml", "[package]\nname = \"c\"");
        var service = CreateService();

        var first = await service.DetectAsync(dir.Path);
        var second = await service.DetectAsync(dir.Path);
        Assert.Same(first, second);

        service.Invalidate(dir.Path);
        var third = await service.DetectAsync(dir.Path);
        Assert.NotSame(first, third); // recomputed after invalidation
        Assert.Equal(first!.PrimaryType, third!.PrimaryType);
    }

    [Fact]
    public async Task MissingFolder_ReturnsNull()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"NexusTest_{Guid.NewGuid():N}_missing");
        var info = await CreateService().DetectAsync(missing);
        Assert.Null(info);
    }

    /// <summary>Disposable temp directory helper used by the tests above.</summary>
    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"NexusTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void WriteFile(string name, string content) =>
            File.WriteAllText(System.IO.Path.Combine(Path, name), content);

        public void CreateDir(string name) =>
            Directory.CreateDirectory(System.IO.Path.Combine(Path, name));

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }
}
