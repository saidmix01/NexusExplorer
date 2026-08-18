using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.FileSystem;

namespace NexusExplorer.Tests;

public class SearchServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileSystemService _fileSystemService;
    private readonly SearchService _sut;

    public SearchServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"NexusSearch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _fileSystemService = new FileSystemService(NullLogger<FileSystemService>.Instance);
        _sut = new SearchService(_fileSystemService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task SearchAsync_ByFilename_FindsFile()
    {
        File.WriteAllText(Path.Combine(_testDir, "test.txt"), "hello");
        File.WriteAllText(Path.Combine(_testDir, "other.md"), "hello");

        var results = new List<FileSystemItem>();
        await foreach (var item in _sut.SearchAsync(_testDir, "test"))
        {
            results.Add(item);
        }

        Assert.Single(results);
        Assert.Equal("test.txt", results[0].Name);
    }

    [Fact]
    public async Task SearchAsync_ByExtension_FindsFiles()
    {
        File.WriteAllText(Path.Combine(_testDir, "test1.txt"), "hello");
        File.WriteAllText(Path.Combine(_testDir, "test2.txt"), "hello");
        File.WriteAllText(Path.Combine(_testDir, "other.md"), "hello");

        var results = new List<FileSystemItem>();
        await foreach (var item in _sut.SearchAsync(_testDir, ".txt"))
        {
            results.Add(item);
        }

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(".txt", r.Extension));
    }

    [Fact]
    public async Task SearchAsync_Recursive_FindsFilesInSubdirectories()
    {
        var subDir = Path.Combine(_testDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "deep.txt"), "hello");

        var results = new List<FileSystemItem>();
        await foreach (var item in _sut.SearchAsync(_testDir, "deep"))
        {
            results.Add(item);
        }

        Assert.Single(results);
        Assert.Equal("deep.txt", results[0].Name);
    }
}
