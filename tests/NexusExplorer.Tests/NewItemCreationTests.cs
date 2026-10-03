using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.FileOperations;

namespace NexusExplorer.Tests;

/// <summary>
/// Verifies that the file-operation layer materializes a <see cref="NewItemDefinition"/> correctly
/// for each creation kind, and that duplicate names are auto-resolved so the "New" action never
/// fails on a clash. Pure file-system logic — no registry involved.
/// </summary>
public class NewItemCreationTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileOperationService _sut;

    public NewItemCreationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"NexusNewItem_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _sut = new FileOperationService(new StubRecycleBinService(), NullLogger<FileOperationService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task CreateFromDefinition_EmptyFile_CreatesNamedFile()
    {
        var def = new NewItemDefinition
        {
            DisplayName = "Text Document",
            Kind = NewItemKind.EmptyFile,
            Extension = ".txt",
            DefaultBaseName = "New Text Document",
        };

        var (result, path) = await _sut.CreateFromDefinitionAsync(_testDir, def);

        Assert.True(result.Success);
        Assert.NotNull(path);
        Assert.Equal("New Text Document.txt", Path.GetFileName(path));
        Assert.True(File.Exists(path));
        Assert.Equal(0, new FileInfo(path!).Length);
    }

    [Fact]
    public async Task CreateFromDefinition_DuplicateName_AutoIncrements()
    {
        var def = new NewItemDefinition
        {
            DisplayName = "Text Document",
            Kind = NewItemKind.EmptyFile,
            Extension = ".txt",
            DefaultBaseName = "New Text Document",
        };

        var (_, first) = await _sut.CreateFromDefinitionAsync(_testDir, def);
        var (result, second) = await _sut.CreateFromDefinitionAsync(_testDir, def);

        Assert.True(result.Success);
        Assert.Equal("New Text Document.txt", Path.GetFileName(first));
        Assert.Equal("New Text Document (2).txt", Path.GetFileName(second));
    }

    [Fact]
    public async Task CreateFromDefinition_TemplateFile_CopiesTemplateContent()
    {
        var templatePath = Path.Combine(_testDir, "template.docx");
        File.WriteAllText(templatePath, "TEMPLATE-CONTENT");

        var def = new NewItemDefinition
        {
            DisplayName = "Word Document",
            Kind = NewItemKind.TemplateFile,
            Extension = ".docx",
            DefaultBaseName = "New Word Document",
            TemplatePath = templatePath,
        };

        var (result, path) = await _sut.CreateFromDefinitionAsync(_testDir, def);

        Assert.True(result.Success);
        Assert.Equal("New Word Document.docx", Path.GetFileName(path));
        Assert.Equal("TEMPLATE-CONTENT", File.ReadAllText(path!));
    }

    [Fact]
    public async Task CreateFromDefinition_TemplateMissing_FallsBackToEmptyFile()
    {
        var def = new NewItemDefinition
        {
            DisplayName = "Word Document",
            Kind = NewItemKind.TemplateFile,
            Extension = ".docx",
            DefaultBaseName = "New Word Document",
            TemplatePath = Path.Combine(_testDir, "does-not-exist.docx"),
        };

        var (result, path) = await _sut.CreateFromDefinitionAsync(_testDir, def);

        Assert.True(result.Success);
        Assert.True(File.Exists(path));
        Assert.Equal(0, new FileInfo(path!).Length);
    }

    [Fact]
    public async Task CreateFromDefinition_DataFile_WritesBytes()
    {
        var def = new NewItemDefinition
        {
            DisplayName = "Icon File",
            Kind = NewItemKind.DataFile,
            Extension = ".ico",
            DefaultBaseName = "New Icon File",
            Data = [10, 20, 30],
        };

        var (result, path) = await _sut.CreateFromDefinitionAsync(_testDir, def);

        Assert.True(result.Success);
        Assert.Equal([10, 20, 30], File.ReadAllBytes(path!));
    }

    [Fact]
    public async Task CreateFromDefinition_MissingParentDirectory_Fails()
    {
        var def = new NewItemDefinition
        {
            DisplayName = "Text Document",
            Kind = NewItemKind.EmptyFile,
            Extension = ".txt",
            DefaultBaseName = "New Text Document",
        };

        var (result, path) = await _sut.CreateFromDefinitionAsync(
            Path.Combine(_testDir, "no-such-folder"), def);

        Assert.False(result.Success);
        Assert.Null(path);
    }

    [Fact]
    public async Task CreateFileAsync_AutoResolve_IncrementsInsteadOfFailing()
    {
        var (_, _) = await _sut.CreateFileAsync(_testDir, "note.txt");
        var (result, path) = await _sut.CreateFileAsync(_testDir, "note.txt", autoResolveConflicts: true);

        Assert.True(result.Success);
        Assert.Equal("note (2).txt", Path.GetFileName(path));
    }

    [Fact]
    public async Task CreateFileAsync_WithoutAutoResolve_StillFailsOnDuplicate()
    {
        await _sut.CreateFileAsync(_testDir, "note.txt");
        var (result, _) = await _sut.CreateFileAsync(_testDir, "note.txt");

        Assert.False(result.Success);
    }

    /// <summary>No-op recycle bin: creation tests never delete.</summary>
    private sealed class StubRecycleBinService : IRecycleBinService
    {
        public bool IsSupported => false;
        public Task<bool> RecycleAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
