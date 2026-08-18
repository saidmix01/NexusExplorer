using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.FileOperations;

namespace NexusExplorer.Tests;

public class FileOperationServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileOperationService _sut;

    public FileOperationServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"NexusFileOps_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        var recycleBin = new TestRecycleBinService();
        _sut = new FileOperationService(recycleBin, NullLogger<FileOperationService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    // --- Copy ---

    [Fact]
    public async Task CopyAsync_SingleFile_CopiesSuccessfully()
    {
        var src = CreateFile("source.txt", "hello");
        var destDir = CreateSubDir("dest");

        var result = await _sut.CopyAsync([src], destDir);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(destDir, "source.txt")));
        Assert.True(File.Exists(src)); // source still exists
    }

    [Fact]
    public async Task CopyAsync_MultipleFiles_CopiesAll()
    {
        var src1 = CreateFile("a.txt", "aaa");
        var src2 = CreateFile("b.txt", "bbb");
        var destDir = CreateSubDir("dest");

        var result = await _sut.CopyAsync([src1, src2], destDir);

        Assert.True(result.Success);
        Assert.Equal(2, result.ItemsProcessed);
        Assert.True(File.Exists(Path.Combine(destDir, "a.txt")));
        Assert.True(File.Exists(Path.Combine(destDir, "b.txt")));
    }

    [Fact]
    public async Task CopyAsync_Directory_CopiesRecursively()
    {
        var srcDir = CreateSubDir("srcFolder");
        File.WriteAllText(Path.Combine(srcDir, "file.txt"), "data");
        var subDir = Path.Combine(srcDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested");

        var destDir = CreateSubDir("dest");

        var result = await _sut.CopyAsync([srcDir], destDir);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(destDir, "srcFolder", "file.txt")));
        Assert.True(File.Exists(Path.Combine(destDir, "srcFolder", "sub", "nested.txt")));
    }

    [Fact]
    public async Task CopyAsync_Conflict_WithSkip_SkipsFile()
    {
        var src = CreateFile("file.txt", "new content");
        var destDir = CreateSubDir("dest");
        File.WriteAllText(Path.Combine(destDir, "file.txt"), "old content");

        var result = await _sut.CopyAsync([src], destDir,
            conflictResolver: _ => Task.FromResult(ConflictAction.Skip));

        Assert.True(result.Success);
        Assert.Equal("old content", File.ReadAllText(Path.Combine(destDir, "file.txt")));
    }

    [Fact]
    public async Task CopyAsync_Conflict_WithReplace_OverwritesFile()
    {
        var src = CreateFile("file.txt", "new content");
        var destDir = CreateSubDir("dest");
        File.WriteAllText(Path.Combine(destDir, "file.txt"), "old content");

        var result = await _sut.CopyAsync([src], destDir,
            conflictResolver: _ => Task.FromResult(ConflictAction.Replace));

        Assert.True(result.Success);
        Assert.Equal("new content", File.ReadAllText(Path.Combine(destDir, "file.txt")));
    }

    [Fact]
    public async Task CopyAsync_Conflict_WithRenameAutomatically_RenamesFile()
    {
        var src = CreateFile("file.txt", "new content");
        var destDir = CreateSubDir("dest");
        File.WriteAllText(Path.Combine(destDir, "file.txt"), "old content");

        var result = await _sut.CopyAsync([src], destDir,
            conflictResolver: _ => Task.FromResult(ConflictAction.RenameAutomatically));

        Assert.True(result.Success);
        Assert.Equal("old content", File.ReadAllText(Path.Combine(destDir, "file.txt")));
        Assert.Equal("new content", File.ReadAllText(Path.Combine(destDir, "file (1).txt")));
    }
    [Fact]
    public async Task CopyAsync_Cancellation_StopsOperation()
    {
        var src = CreateFile("file.txt", new string('x', 10_000));
        var destDir = CreateSubDir("dest");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Pre-cancelled token: either returns Cancelled result or throws OperationCanceledException
        try
        {
            var result = await _sut.CopyAsync([src], destDir, cancellationToken: cts.Token);
            Assert.True(result.Cancelled);
        }
        catch (OperationCanceledException)
        {
            // Also acceptable — Task.Run with pre-cancelled token may throw
        }
    }

    // --- Move ---

    [Fact]
    public async Task MoveAsync_SingleFile_MovesSuccessfully()
    {
        var src = CreateFile("move_me.txt", "data");
        var destDir = CreateSubDir("dest");

        var result = await _sut.MoveAsync([src], destDir);

        Assert.True(result.Success);
        Assert.False(File.Exists(src)); // source removed
        Assert.True(File.Exists(Path.Combine(destDir, "move_me.txt")));
    }

    [Fact]
    public async Task MoveAsync_MultipleItems_MovesAll()
    {
        var src1 = CreateFile("x.txt", "x");
        var src2 = CreateFile("y.txt", "y");
        var destDir = CreateSubDir("dest");

        var result = await _sut.MoveAsync([src1, src2], destDir);

        Assert.True(result.Success);
        Assert.Equal(2, result.ItemsProcessed);
    }

    [Fact]
    public async Task MoveAsync_Conflict_WithRenameAutomatically_RenamesFile()
    {
        var src = CreateFile("file.txt", "new content");
        var destDir = CreateSubDir("dest");
        File.WriteAllText(Path.Combine(destDir, "file.txt"), "old content");

        var result = await _sut.MoveAsync([src], destDir,
            conflictResolver: _ => Task.FromResult(ConflictAction.RenameAutomatically));

        Assert.True(result.Success);
        Assert.False(File.Exists(src));
        Assert.Equal("old content", File.ReadAllText(Path.Combine(destDir, "file.txt")));
        Assert.Equal("new content", File.ReadAllText(Path.Combine(destDir, "file (1).txt")));
    }

    // --- Rename ---

    [Fact]
    public async Task RenameAsync_File_RenamesSuccessfully()
    {
        var src = CreateFile("old_name.txt", "content");

        var result = await _sut.RenameAsync(src, "new_name.txt");

        Assert.True(result.Success);
        Assert.False(File.Exists(src));
        Assert.True(File.Exists(Path.Combine(_testDir, "new_name.txt")));
    }

    [Fact]
    public async Task RenameAsync_InvalidChars_ReturnsError()
    {
        var src = CreateFile("file.txt", "content");

        var result = await _sut.RenameAsync(src, "invalid<>name.txt");

        Assert.False(result.Success);
        Assert.Contains("invalid characters", result.Error);
    }

    [Fact]
    public async Task RenameAsync_EmptyName_ReturnsError()
    {
        var src = CreateFile("file.txt", "content");

        var result = await _sut.RenameAsync(src, "");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RenameAsync_ConflictingName_ReturnsError()
    {
        CreateFile("existing.txt", "a");
        var src = CreateFile("other.txt", "b");

        var result = await _sut.RenameAsync(src, "existing.txt");

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);
    }

    // --- Delete ---

    [Fact]
    public async Task DeleteAsync_File_DeletesSuccessfully()
    {
        var src = CreateFile("delete_me.txt", "bye");

        var result = await _sut.DeleteAsync([src], useRecycleBin: false);

        Assert.True(result.Success);
        Assert.False(File.Exists(src));
    }

    [Fact]
    public async Task DeleteAsync_Directory_DeletesRecursively()
    {
        var dir = CreateSubDir("deleteDir");
        File.WriteAllText(Path.Combine(dir, "inner.txt"), "data");

        var result = await _sut.DeleteAsync([dir], useRecycleBin: false);

        Assert.True(result.Success);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task DeleteAsync_MultipleItems_DeletesAll()
    {
        var f1 = CreateFile("del1.txt", "x");
        var f2 = CreateFile("del2.txt", "y");

        var result = await _sut.DeleteAsync([f1, f2], useRecycleBin: false);

        Assert.True(result.Success);
        Assert.Equal(2, result.ItemsProcessed);
    }

    // --- CreateDirectory ---

    [Fact]
    public async Task CreateDirectoryAsync_CreatesNewFolder()
    {
        var (result, path) = await _sut.CreateDirectoryAsync(_testDir);

        Assert.True(result.Success);
        Assert.NotNull(path);
        Assert.True(Directory.Exists(path));
        Assert.Equal("New Folder", Path.GetFileName(path));
    }

    [Fact]
    public async Task CreateDirectoryAsync_ConflictResolution_IncrementsNumber()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "New Folder"));

        var (result, path) = await _sut.CreateDirectoryAsync(_testDir);

        Assert.True(result.Success);
        Assert.Equal("New Folder (2)", Path.GetFileName(path));
    }

    [Fact]
    public async Task CreateDirectoryAsync_MultipleConflicts_KeepsIncrementing()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "New Folder"));
        Directory.CreateDirectory(Path.Combine(_testDir, "New Folder (2)"));

        var (result, path) = await _sut.CreateDirectoryAsync(_testDir);

        Assert.True(result.Success);
        Assert.Equal("New Folder (3)", Path.GetFileName(path));
    }

    // --- CreateFile ---

    [Fact]
    public async Task CreateFileAsync_CreatesNewFile()
    {
        var (result, path) = await _sut.CreateFileAsync(_testDir, "test.txt");

        Assert.True(result.Success);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.Equal("test.txt", Path.GetFileName(path));
    }

    [Fact]
    public async Task CreateFileAsync_Conflict_ReturnsError()
    {
        CreateFile("test.txt", "data");

        var (result, path) = await _sut.CreateFileAsync(_testDir, "test.txt");

        Assert.False(result.Success);
        Assert.Null(path);
    }

    // --- Clipboard ---

    [Fact]
    public void ClipboardService_SetCopy_StoresPaths()
    {
        var sut = new ClipboardService();
        sut.SetCopy([@"C:\a.txt", @"C:\b.txt"]);

        Assert.True(sut.HasContent);
        Assert.NotNull(sut.Current);
        Assert.False(sut.Current.IsCut);
        Assert.Equal(2, sut.Current.Paths.Count);
    }

    [Fact]
    public void ClipboardService_SetCut_StoresAsCut()
    {
        var sut = new ClipboardService();
        sut.SetCut([@"C:\file.txt"]);

        Assert.True(sut.HasContent);
        Assert.True(sut.Current!.IsCut);
    }

    [Fact]
    public void ClipboardService_Clear_RemovesContent()
    {
        var sut = new ClipboardService();
        sut.SetCopy([@"C:\file.txt"]);
        sut.Clear();

        Assert.False(sut.HasContent);
        Assert.Null(sut.Current);
    }

    [Fact]
    public void ClipboardService_FiresChangedEvent()
    {
        var sut = new ClipboardService();
        var fired = false;
        sut.ClipboardChanged += (_, _) => fired = true;

        sut.SetCopy([@"C:\test"]);

        Assert.True(fired);
    }

    // --- Progress ---

    [Fact]
    public async Task CopyAsync_ReportsProgress()
    {
        var src = CreateFile("progress.txt", new string('x', 1000));
        var destDir = CreateSubDir("dest");
        var progressReports = new List<FileOperationProgress>();

        await _sut.CopyAsync([src], destDir,
            progress: new Progress<FileOperationProgress>(p => progressReports.Add(p)));

        // Give progress callbacks time to fire
        await Task.Delay(100);
        Assert.NotEmpty(progressReports);
        Assert.Contains(progressReports, p => p.IsCompleted);
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

    private sealed class TestRecycleBinService : IRecycleBinService
    {
        public bool IsSupported => false;
        public Task<bool> RecycleAsync(string path, CancellationToken ct) => Task.FromResult(false);
    }
}
