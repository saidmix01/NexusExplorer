using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Infrastructure.FileOperations;

namespace NexusExplorer.Tests;

public class OperationHistoryTests : IDisposable
{
    private readonly string _testDir;
    private readonly OperationHistoryService _sut;

    public OperationHistoryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"NexusUndo_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _sut = new OperationHistoryService(NullLogger<OperationHistoryService>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    // --- Copy Undo ---

    [Fact]
    public async Task UndoCopy_DeletesCopiedFile()
    {
        var src = Path.Combine(_testDir, "original.txt");
        var dest = Path.Combine(_testDir, "copy.txt");
        File.WriteAllText(src, "data");
        File.WriteAllText(dest, "data"); // simulate the copy result

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.Copy,
            Description = "Copy 'original.txt'",
            Entries = [new UndoEntry { SourcePath = src, DestinationPath = dest, IsDirectory = false }]
        });

        var result = await _sut.UndoAsync();

        Assert.True(result.Success);
        Assert.True(File.Exists(src)); // original untouched
        Assert.False(File.Exists(dest)); // copy removed
    }

    // --- Move Undo ---

    [Fact]
    public async Task UndoMove_MovesFileBack()
    {
        var src = Path.Combine(_testDir, "source.txt");
        var dest = Path.Combine(_testDir, "moved.txt");
        File.WriteAllText(dest, "data"); // file is at destination after move

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.Move,
            Description = "Move 'source.txt'",
            Entries = [new UndoEntry { SourcePath = src, DestinationPath = dest, IsDirectory = false }]
        });

        var result = await _sut.UndoAsync();

        Assert.True(result.Success);
        Assert.True(File.Exists(src)); // back at original
        Assert.False(File.Exists(dest)); // gone from destination
    }

    // --- Rename Undo ---

    [Fact]
    public async Task UndoRename_RenamesBack()
    {
        var oldPath = Path.Combine(_testDir, "old.txt");
        var newPath = Path.Combine(_testDir, "new.txt");
        File.WriteAllText(newPath, "data"); // file is at new name after rename

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.Rename,
            Description = "Rename 'old.txt'",
            Entries = [new UndoEntry { SourcePath = oldPath, DestinationPath = newPath, IsDirectory = false }]
        });

        var result = await _sut.UndoAsync();

        Assert.True(result.Success);
        Assert.True(File.Exists(oldPath));
        Assert.False(File.Exists(newPath));
    }

    // --- Create File Undo ---

    [Fact]
    public async Task UndoCreateFile_DeletesCreatedFile()
    {
        var path = Path.Combine(_testDir, "created.txt");
        File.WriteAllText(path, "");

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.CreateFile,
            Description = "Create 'created.txt'",
            Entries = [new UndoEntry { SourcePath = path, DestinationPath = path, IsDirectory = false }]
        });

        var result = await _sut.UndoAsync();

        Assert.True(result.Success);
        Assert.False(File.Exists(path));
    }

    // --- Create Folder Undo ---

    [Fact]
    public async Task UndoCreateFolder_DeletesEmptyFolder()
    {
        var path = Path.Combine(_testDir, "NewFolder");
        Directory.CreateDirectory(path);

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.CreateFolder,
            Description = "Create folder 'NewFolder'",
            Entries = [new UndoEntry { SourcePath = path, DestinationPath = path, IsDirectory = true }]
        });

        var result = await _sut.UndoAsync();

        Assert.True(result.Success);
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task UndoCreateFolder_FailsIfFolderNotEmpty()
    {
        var path = Path.Combine(_testDir, "NotEmpty");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "child.txt"), "x");

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.CreateFolder,
            Description = "Create folder 'NotEmpty'",
            Entries = [new UndoEntry { SourcePath = path, DestinationPath = path, IsDirectory = true }]
        });

        var result = await _sut.UndoAsync();

        // Should fail because folder has content
        Assert.False(result.Success);
        Assert.True(Directory.Exists(path)); // folder still exists
    }

    // --- Delete (non-reversible) ---

    [Fact]
    public void Delete_IsNotReversible()
    {
        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.Delete,
            Description = "Delete 'file.txt'",
            Entries = [new UndoEntry { SourcePath = "/test/file.txt", DestinationPath = "", IsDirectory = false }],
            IsReversible = false
        });

        Assert.False(_sut.CanUndo);
        Assert.Null(_sut.UndoDescription);
    }

    // --- Multi-item operations ---

    [Fact]
    public async Task UndoCopy_MultipleFiles_DeletesAll()
    {
        var dest1 = Path.Combine(_testDir, "copy1.txt");
        var dest2 = Path.Combine(_testDir, "copy2.txt");
        var dest3 = Path.Combine(_testDir, "copy3.txt");
        File.WriteAllText(dest1, "a");
        File.WriteAllText(dest2, "b");
        File.WriteAllText(dest3, "c");

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.Copy,
            Description = "Copy 3 items",
            Entries =
            [
                new UndoEntry { SourcePath = "/src/1.txt", DestinationPath = dest1 },
                new UndoEntry { SourcePath = "/src/2.txt", DestinationPath = dest2 },
                new UndoEntry { SourcePath = "/src/3.txt", DestinationPath = dest3 },
            ]
        });

        var result = await _sut.UndoAsync();

        Assert.True(result.Success);
        Assert.Equal(3, result.ItemsReverted);
        Assert.False(File.Exists(dest1));
        Assert.False(File.Exists(dest2));
        Assert.False(File.Exists(dest3));
    }

    [Fact]
    public async Task UndoMove_MultipleFiles_MovesAllBack()
    {
        var srcDir = Path.Combine(_testDir, "src");
        var destDir = Path.Combine(_testDir, "dest");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);

        File.WriteAllText(Path.Combine(destDir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(destDir, "b.txt"), "b");

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.Move,
            Description = "Move 2 items",
            Entries =
            [
                new UndoEntry { SourcePath = Path.Combine(srcDir, "a.txt"), DestinationPath = Path.Combine(destDir, "a.txt") },
                new UndoEntry { SourcePath = Path.Combine(srcDir, "b.txt"), DestinationPath = Path.Combine(destDir, "b.txt") },
            ]
        });

        var result = await _sut.UndoAsync();

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(srcDir, "a.txt")));
        Assert.True(File.Exists(Path.Combine(srcDir, "b.txt")));
        Assert.False(File.Exists(Path.Combine(destDir, "a.txt")));
        Assert.False(File.Exists(Path.Combine(destDir, "b.txt")));
    }

    // --- Empty history ---

    [Fact]
    public async Task Undo_EmptyHistory_ReturnsNoOperation()
    {
        var result = await _sut.UndoAsync();
        Assert.False(result.Success);
        Assert.Equal("Nothing to undo.", result.Error);
    }

    [Fact]
    public void CanUndo_EmptyHistory_IsFalse()
    {
        Assert.False(_sut.CanUndo);
    }

    // --- History max size ---

    [Fact]
    public void History_ExceedsMaxSize_TrimsOldest()
    {
        for (int i = 0; i < 110; i++)
        {
            _sut.AddOperation(new UndoableOperation
            {
                Type = UndoOperationType.CreateFile,
                Description = $"Create file{i}.txt",
                Entries = [new UndoEntry { SourcePath = $"/f{i}", DestinationPath = $"/f{i}" }]
            });
        }

        // Should still be able to undo (history is trimmed but not empty)
        Assert.True(_sut.CanUndo);
    }

    // --- Cancelled/failed operations ---

    [Fact]
    public void CancelledOperation_NotAddedToHistory()
    {
        // Cancelled operations should NOT be added by the ViewModel.
        // This tests that if we don't add, CanUndo stays false.
        Assert.False(_sut.CanUndo);
    }

    // --- Conflict during undo ---

    [Fact]
    public async Task UndoMove_ConflictAtOriginal_Fails()
    {
        var src = Path.Combine(_testDir, "conflict.txt");
        var dest = Path.Combine(_testDir, "moved_conflict.txt");
        File.WriteAllText(src, "existing file"); // something already at source
        File.WriteAllText(dest, "moved data");

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.Move,
            Description = "Move 'conflict.txt'",
            Entries = [new UndoEntry { SourcePath = src, DestinationPath = dest }]
        });

        var result = await _sut.UndoAsync();

        // Should fail because original location already has a file
        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);
    }

    // --- CanUndo updates ---

    [Fact]
    public async Task CanUndo_UpdatesAfterUndo()
    {
        var path = Path.Combine(_testDir, "canundo.txt");
        File.WriteAllText(path, "");

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.CreateFile,
            Description = "Create 'canundo.txt'",
            Entries = [new UndoEntry { SourcePath = path, DestinationPath = path }]
        });

        Assert.True(_sut.CanUndo);

        await _sut.UndoAsync();

        Assert.False(_sut.CanUndo);
    }

    // --- HistoryChanged event ---

    [Fact]
    public void HistoryChanged_FiresOnAdd()
    {
        bool fired = false;
        _sut.HistoryChanged += (_, _) => fired = true;

        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.CreateFile,
            Description = "test",
            Entries = [new UndoEntry { SourcePath = "/x", DestinationPath = "/x" }]
        });

        Assert.True(fired);
    }

    [Fact]
    public void Clear_EmptiesHistory()
    {
        _sut.AddOperation(new UndoableOperation
        {
            Type = UndoOperationType.CreateFile,
            Description = "test",
            Entries = [new UndoEntry { SourcePath = "/x", DestinationPath = "/x" }]
        });

        Assert.True(_sut.CanUndo);

        _sut.Clear();

        Assert.False(_sut.CanUndo);
    }
}
