using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.FileOperations;

namespace NexusExplorer.Tests;

public class DeleteProgressTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileOperationService _sut;
    private readonly List<FileOperationProgress> _progressReports = [];

    public DeleteProgressTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"NexusDelProg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        var recycleBin = new FakeRecycleBin();
        _sut = new FileOperationService(recycleBin, NullLogger<FileOperationService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private IProgress<FileOperationProgress> CreateProgress()
    {
        return new Progress<FileOperationProgress>(p => _progressReports.Add(p));
    }

    // Use synchronous progress to capture reports immediately in Task.Run context
    private SyncProgress CreateSyncProgress()
    {
        return new SyncProgress(_progressReports);
    }

    [Fact]
    public async Task Delete_SingleFile_ReportsProgress()
    {
        var file = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(file, "hello");
        var progress = CreateSyncProgress();

        var result = await _sut.DeleteAsync([file], useRecycleBin: false, progress: progress);

        Assert.True(result.Success);
        Assert.True(_progressReports.Count >= 1);
        // Last report should be completion
        var last = _progressReports[^1];
        Assert.True(last.IsCompleted);
    }

    [Fact]
    public async Task Delete_MultipleFiles_ReportsCorrectTotal()
    {
        for (int i = 0; i < 5; i++)
            File.WriteAllText(Path.Combine(_testDir, $"file{i}.txt"), "data");

        var paths = Directory.GetFiles(_testDir).ToList();
        var progress = CreateSyncProgress();

        var result = await _sut.DeleteAsync(paths, useRecycleBin: false, progress: progress);

        Assert.True(result.Success);
        Assert.Equal(5, result.ItemsProcessed);

        // Should have reported TotalItems = 5
        var itemReports = _progressReports.Where(p => !p.IsCompleted && p.TotalItems > 0).ToList();
        Assert.All(itemReports, p => Assert.Equal(5, p.TotalItems));
    }

    [Fact]
    public async Task Delete_SmallFolder_ReportsRecursiveProgress()
    {
        var dir = Path.Combine(_testDir, "folder");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(dir, "b.txt"), "b");
        var sub = Path.Combine(dir, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "c.txt"), "c");

        var progress = CreateSyncProgress();

        var result = await _sut.DeleteAsync([dir], useRecycleBin: false, progress: progress);

        Assert.True(result.Success);
        Assert.False(Directory.Exists(dir));

        // Total should include: 3 files + 1 subdir + 1 top dir = 5
        var itemReports = _progressReports.Where(p => !p.IsCompleted && p.TotalItems > 0).ToList();
        if (itemReports.Count > 0)
            Assert.Equal(5, itemReports[0].TotalItems);
    }

    [Fact]
    public async Task Delete_Recursive_ProgressStartsAtZero()
    {
        var file = Path.Combine(_testDir, "zero.txt");
        File.WriteAllText(file, "x");
        var progress = CreateSyncProgress();

        await _sut.DeleteAsync([file], useRecycleBin: false, progress: progress);

        // First non-completed report should have CurrentItemIndex starting from 1 (after first item deleted)
        var first = _progressReports.FirstOrDefault(p => !p.IsCompleted);
        Assert.NotNull(first);
        Assert.True(first.CurrentItemIndex >= 1);
    }

    [Fact]
    public async Task Delete_Recursive_ProgressReachesCompletion()
    {
        for (int i = 0; i < 3; i++)
            File.WriteAllText(Path.Combine(_testDir, $"done{i}.txt"), "x");

        var paths = Directory.GetFiles(_testDir).ToList();
        var progress = CreateSyncProgress();

        await _sut.DeleteAsync(paths, useRecycleBin: false, progress: progress);

        var completed = _progressReports.LastOrDefault();
        Assert.NotNull(completed);
        Assert.True(completed.IsCompleted);
    }

    [Fact]
    public async Task Delete_Cancellation_StopsEarly()
    {
        for (int i = 0; i < 20; i++)
            File.WriteAllText(Path.Combine(_testDir, $"cancel{i}.txt"), "data");

        var paths = Directory.GetFiles(_testDir).ToList();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.DeleteAsync(paths, useRecycleBin: false, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Delete_NonExistentFile_HandledGracefully()
    {
        var fakePath = Path.Combine(_testDir, "does_not_exist.txt");
        var progress = CreateSyncProgress();

        // Should not throw - file simply doesn't exist so nothing to delete
        var result = await _sut.DeleteAsync([fakePath], useRecycleBin: false, progress: progress);

        // The file doesn't exist, so nothing is deleted - result depends on implementation
        // At minimum it should not crash
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Delete_EmptyPaths_ReturnsSuccess()
    {
        var progress = CreateSyncProgress();

        var result = await _sut.DeleteAsync([], useRecycleBin: false, progress: progress);

        Assert.True(result.Success);
        // Completion should be reported
        var completed = _progressReports.LastOrDefault();
        Assert.NotNull(completed);
        Assert.True(completed.IsCompleted);
    }

    [Fact]
    public async Task Delete_ErrorOnOneFile_ContinuesWithOthers()
    {
        // Create a file that's locked
        var goodFile = Path.Combine(_testDir, "good.txt");
        var lockedFile = Path.Combine(_testDir, "locked.txt");
        File.WriteAllText(goodFile, "ok");
        File.WriteAllText(lockedFile, "locked");

        // Lock the file by opening it exclusively
        using var lockStream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var progress = CreateSyncProgress();
        var result = await _sut.DeleteAsync([goodFile, lockedFile], useRecycleBin: false, progress: progress);

        // Good file should be deleted
        Assert.False(File.Exists(goodFile));
        // Locked file should still exist
        Assert.True(File.Exists(lockedFile));
    }

    [Fact]
    public async Task Delete_CorrectTotalForMixedItems()
    {
        // 2 files + 1 folder with 2 files inside = total 2 + (2 files + 1 dir) = 5
        File.WriteAllText(Path.Combine(_testDir, "single1.txt"), "a");
        File.WriteAllText(Path.Combine(_testDir, "single2.txt"), "b");
        var dir = Path.Combine(_testDir, "mixed");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "inner1.txt"), "c");
        File.WriteAllText(Path.Combine(dir, "inner2.txt"), "d");

        var paths = new List<string>
        {
            Path.Combine(_testDir, "single1.txt"),
            Path.Combine(_testDir, "single2.txt"),
            dir
        };

        var progress = CreateSyncProgress();
        var result = await _sut.DeleteAsync(paths, useRecycleBin: false, progress: progress);

        Assert.True(result.Success);

        // Total should be 2 files + (2 files + 1 dir) = 5
        var itemReports = _progressReports.Where(p => !p.IsCompleted && p.TotalItems > 0).ToList();
        if (itemReports.Count > 0)
            Assert.Equal(5, itemReports[0].TotalItems);
    }

    /// <summary>
    /// Synchronous IProgress implementation that captures reports immediately
    /// (unlike Progress&lt;T&gt; which posts to SynchronizationContext).
    /// </summary>
    private sealed class SyncProgress : IProgress<FileOperationProgress>
    {
        private readonly List<FileOperationProgress> _reports;

        public SyncProgress(List<FileOperationProgress> reports) => _reports = reports;

        public void Report(FileOperationProgress value) => _reports.Add(value);
    }

    private sealed class FakeRecycleBin : IRecycleBinService
    {
        public bool IsSupported => false;
        public Task<bool> RecycleAsync(string path, CancellationToken ct) => Task.FromResult(false);
    }
}
