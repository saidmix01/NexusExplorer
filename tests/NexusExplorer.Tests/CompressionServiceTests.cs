using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.FileOperations;

namespace NexusExplorer.Tests;

public class CompressionServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly CompressionService _sut;

    public CompressionServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"NexusCompress_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _sut = new CompressionService(NullLogger<CompressionService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    // --- Test 6: Single file to ZIP ---

    [Fact]
    public async Task CompressAsync_SingleFile_CreatesZip()
    {
        var filePath = CreateFile("document.txt", "Hello World");

        var result = await _sut.CompressAsync([filePath]);

        Assert.True(result.Success);
        Assert.NotNull(result.ArchivePath);
        Assert.Equal(Path.Combine(_testDir, "document.zip"), result.ArchivePath);
        Assert.True(File.Exists(result.ArchivePath));

        // Verify content
        using var archive = ZipFile.OpenRead(result.ArchivePath);
        Assert.Single(archive.Entries);
        Assert.Equal("document.txt", archive.Entries[0].FullName);
    }

    // --- Test 7: Folder to ZIP ---

    [Fact]
    public async Task CompressAsync_Folder_CreatesZipWithContents()
    {
        var folderPath = CreateSubDir("MyFolder");
        File.WriteAllText(Path.Combine(folderPath, "file1.txt"), "content1");
        File.WriteAllText(Path.Combine(folderPath, "file2.txt"), "content2");
        var subDir = Path.Combine(folderPath, "SubDir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested");

        var result = await _sut.CompressAsync([folderPath]);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_testDir, "MyFolder.zip"), result.ArchivePath);

        using var archive = ZipFile.OpenRead(result.ArchivePath!);
        var entryNames = archive.Entries.Select(e => e.FullName).OrderBy(n => n).ToList();
        Assert.Contains("MyFolder/file1.txt", entryNames);
        Assert.Contains("MyFolder/file2.txt", entryNames);
        Assert.Contains("MyFolder/SubDir/nested.txt", entryNames);
    }

    // --- Test 8: Multiple items to ZIP ---

    [Fact]
    public async Task CompressAsync_MultipleItems_CreatesArchiveZip()
    {
        var file1 = CreateFile("A.txt", "aaa");
        var file2 = CreateFile("B.txt", "bbb");
        var folder = CreateSubDir("Fotos");
        File.WriteAllText(Path.Combine(folder, "image1.jpg"), "img1");
        File.WriteAllText(Path.Combine(folder, "image2.jpg"), "img2");

        var result = await _sut.CompressAsync([file1, file2, folder]);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_testDir, "Archive.zip"), result.ArchivePath);

        using var archive = ZipFile.OpenRead(result.ArchivePath!);
        var entryNames = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("A.txt", entryNames);
        Assert.Contains("B.txt", entryNames);
        Assert.Contains("Fotos/image1.jpg", entryNames);
        Assert.Contains("Fotos/image2.jpg", entryNames);
    }

    // --- Test 9: Existing ZIP name increments ---

    [Fact]
    public async Task CompressAsync_ExistingZipName_IncrementsCounter()
    {
        var filePath = CreateFile("data.txt", "content");

        // Create first ZIP manually
        File.WriteAllText(Path.Combine(_testDir, "data.zip"), "existing");

        var result = await _sut.CompressAsync([filePath]);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_testDir, "data (1).zip"), result.ArchivePath);
        Assert.True(File.Exists(result.ArchivePath));
    }

    // --- Test 10: Maintains directory structure ---

    [Fact]
    public async Task CompressAsync_Folder_MaintainsDirectoryStructure()
    {
        var root = CreateSubDir("Project");
        var src = Path.Combine(root, "src");
        Directory.CreateDirectory(src);
        var tests = Path.Combine(root, "tests");
        Directory.CreateDirectory(tests);
        File.WriteAllText(Path.Combine(src, "main.cs"), "code");
        File.WriteAllText(Path.Combine(tests, "test.cs"), "test");
        File.WriteAllText(Path.Combine(root, "readme.md"), "readme");

        var result = await _sut.CompressAsync([root]);

        Assert.True(result.Success);

        using var archive = ZipFile.OpenRead(result.ArchivePath!);
        var entryNames = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("Project/src/main.cs", entryNames);
        Assert.Contains("Project/tests/test.cs", entryNames);
        Assert.Contains("Project/readme.md", entryNames);
    }

    // --- Test 11: Large file uses streaming (no OutOfMemory) ---

    [Fact]
    public async Task CompressAsync_LargeFile_StreamsWithoutLoadingAll()
    {
        // Create a 5MB file to test streaming behavior
        var filePath = Path.Combine(_testDir, "large.bin");
        using (var fs = new FileStream(filePath, FileMode.Create))
        {
            var buffer = new byte[81920];
            Random.Shared.NextBytes(buffer);
            for (int i = 0; i < 64; i++) // 64 * 80KB = ~5MB
                fs.Write(buffer, 0, buffer.Length);
        }

        var progressReports = new List<FileOperationProgress>();
        var progress = new Progress<FileOperationProgress>(p => progressReports.Add(p));

        var result = await _sut.CompressAsync([filePath], progress);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.ArchivePath));

        // Verify multiple progress reports were made (streaming)
        Assert.True(progressReports.Count > 1, "Should report progress multiple times for large files");
    }

    // --- Test 12: Cancellation ---

    [Fact]
    public async Task CompressAsync_Cancellation_DeletesIncompleteZip()
    {
        // Create several files so we have time to cancel
        for (int i = 0; i < 100; i++)
            CreateFile($"file{i}.txt", new string('x', 10000));

        var files = Directory.GetFiles(_testDir).ToList();
        var cts = new CancellationTokenSource();

        var progress = new Progress<FileOperationProgress>(_ =>
        {
            // Cancel after first progress report
            cts.Cancel();
        });

        var result = await _sut.CompressAsync(files, progress, cts.Token);

        Assert.True(result.Cancelled);
        // The incomplete ZIP should be cleaned up
        var zips = Directory.GetFiles(_testDir, "*.zip");
        Assert.Empty(zips);
    }

    // --- Test 13: Access error ---

    [Fact]
    public async Task CompressAsync_NonExistentPath_CreatesEmptyZipOrReturnsSuccess()
    {
        // When given a non-existent file, it is simply skipped.
        // The result is a valid (empty) archive.
        var fakePath = Path.Combine(_testDir, "nonexistent.txt");

        var result = await _sut.CompressAsync([fakePath]);

        // The service treats missing items as skipped, still produces a ZIP
        Assert.True(result.Success);
        Assert.NotNull(result.ArchivePath);
        Assert.True(File.Exists(result.ArchivePath));
    }

    // --- Helpers ---

    private string CreateFile(string name, string content)
    {
        var path = Path.Combine(_testDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private string CreateSubDir(string name)
    {
        var path = Path.Combine(_testDir, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
