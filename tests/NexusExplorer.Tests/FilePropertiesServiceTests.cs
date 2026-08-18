using NexusExplorer.Core.Abstractions;
using NexusExplorer.Infrastructure.FileSystem;

namespace NexusExplorer.Tests;

public class FilePropertiesServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly FilePropertiesService _service;

    public FilePropertiesServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "NexusPropsTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _service = new FilePropertiesService();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    // --- GetPropertiesAsync: File ---

    [Fact]
    public async Task GetProperties_File_ReturnsCorrectMetadata()
    {
        var filePath = Path.Combine(_testDir, "test.txt");
        await File.WriteAllTextAsync(filePath, "Hello World");

        var props = await _service.GetPropertiesAsync(filePath);

        Assert.Equal("test.txt", props.Name);
        Assert.Equal(filePath, props.Path);
        Assert.Equal(_testDir, props.DirectoryPath);
        Assert.False(props.IsDirectory);
        Assert.Equal("Text File", props.TypeDescription);
        Assert.True(props.Size > 0);
        Assert.NotNull(props.Created);
        Assert.NotNull(props.Modified);
        Assert.NotNull(props.LastAccessed);
    }

    [Fact]
    public async Task GetProperties_File_ReturnsCorrectSize()
    {
        var filePath = Path.Combine(_testDir, "sized.bin");
        var data = new byte[1024];
        await File.WriteAllBytesAsync(filePath, data);

        var props = await _service.GetPropertiesAsync(filePath);

        Assert.Equal(1024, props.Size);
    }

    // --- GetPropertiesAsync: Directory ---

    [Fact]
    public async Task GetProperties_Directory_ReturnsCorrectMetadata()
    {
        var dirPath = Path.Combine(_testDir, "subdir");
        Directory.CreateDirectory(dirPath);

        var props = await _service.GetPropertiesAsync(dirPath);

        Assert.Equal("subdir", props.Name);
        Assert.Equal(dirPath, props.Path);
        Assert.True(props.IsDirectory);
        Assert.Equal("Folder", props.TypeDescription);
        Assert.NotNull(props.Created);
        Assert.NotNull(props.Modified);
    }

    // --- GetPropertiesAsync: Hidden attribute ---

    [Fact]
    public async Task GetProperties_HiddenFile_ReportsHidden()
    {
        var filePath = Path.Combine(_testDir, "hidden.txt");
        File.WriteAllText(filePath, "");
        File.SetAttributes(filePath, FileAttributes.Hidden);

        var props = await _service.GetPropertiesAsync(filePath);

        Assert.True(props.IsHidden);
    }

    // --- GetPropertiesAsync: ReadOnly attribute ---

    [Fact]
    public async Task GetProperties_ReadOnlyFile_ReportsReadOnly()
    {
        var filePath = Path.Combine(_testDir, "readonly.txt");
        File.WriteAllText(filePath, "");
        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        var props = await _service.GetPropertiesAsync(filePath);

        Assert.True(props.IsReadOnly);

        // Cleanup: remove readonly so deletion works
        File.SetAttributes(filePath, FileAttributes.Normal);
    }

    // --- GetPropertiesAsync: Not found ---

    [Fact]
    public async Task GetProperties_NonExistentPath_ThrowsFileNotFound()
    {
        var path = Path.Combine(_testDir, "does_not_exist.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.GetPropertiesAsync(path));
    }

    // --- CalculateDirectorySizeAsync ---

    [Fact]
    public async Task CalculateDirectorySize_EmptyDir_ReturnsZero()
    {
        var dirPath = Path.Combine(_testDir, "emptydir");
        Directory.CreateDirectory(dirPath);

        var result = await _service.CalculateDirectorySizeAsync(dirPath);

        Assert.Equal(0, result.TotalSize);
        Assert.Equal(0, result.FileCount);
        Assert.Equal(0, result.FolderCount);
        Assert.True(result.WasCompleted);
    }

    [Fact]
    public async Task CalculateDirectorySize_WithFiles_ReturnsTotalSize()
    {
        var dirPath = Path.Combine(_testDir, "withfiles");
        Directory.CreateDirectory(dirPath);
        File.WriteAllBytes(Path.Combine(dirPath, "a.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(dirPath, "b.bin"), new byte[200]);

        var result = await _service.CalculateDirectorySizeAsync(dirPath);

        Assert.Equal(300, result.TotalSize);
        Assert.Equal(2, result.FileCount);
        Assert.Equal(0, result.FolderCount);
    }

    [Fact]
    public async Task CalculateDirectorySize_Recursive_IncludesSubdirectories()
    {
        var dirPath = Path.Combine(_testDir, "recursive");
        Directory.CreateDirectory(dirPath);
        var subDir = Path.Combine(dirPath, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllBytes(Path.Combine(dirPath, "top.bin"), new byte[50]);
        File.WriteAllBytes(Path.Combine(subDir, "nested.bin"), new byte[75]);

        var result = await _service.CalculateDirectorySizeAsync(dirPath);

        Assert.Equal(125, result.TotalSize);
        Assert.Equal(2, result.FileCount);
        Assert.Equal(1, result.FolderCount);
    }

    [Fact]
    public async Task CalculateDirectorySize_CountsFilesAndFolders()
    {
        var dirPath = Path.Combine(_testDir, "counts");
        Directory.CreateDirectory(dirPath);
        Directory.CreateDirectory(Path.Combine(dirPath, "dir1"));
        Directory.CreateDirectory(Path.Combine(dirPath, "dir2"));
        File.WriteAllText(Path.Combine(dirPath, "f1.txt"), "a");
        File.WriteAllText(Path.Combine(dirPath, "f2.txt"), "b");
        File.WriteAllText(Path.Combine(dirPath, "f3.txt"), "c");

        var result = await _service.CalculateDirectorySizeAsync(dirPath);

        Assert.Equal(3, result.FileCount);
        Assert.Equal(2, result.FolderCount);
    }

    [Fact]
    public async Task CalculateDirectorySize_Cancellation_StopsEarly()
    {
        var dirPath = Path.Combine(_testDir, "cancel");
        Directory.CreateDirectory(dirPath);
        for (int i = 0; i < 10; i++)
            File.WriteAllBytes(Path.Combine(dirPath, $"f{i}.bin"), new byte[10]);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.CalculateDirectorySizeAsync(dirPath, cancellationToken: cts.Token));
    }

    // --- SetAttributesAsync ---

    [Fact]
    public async Task SetAttributes_SetHidden_AppliesAttribute()
    {
        var filePath = Path.Combine(_testDir, "tohide.txt");
        File.WriteAllText(filePath, "");

        await _service.SetAttributesAsync(filePath, isReadOnly: false, isHidden: true);

        var attrs = File.GetAttributes(filePath);
        Assert.True(attrs.HasFlag(FileAttributes.Hidden));
        Assert.False(attrs.HasFlag(FileAttributes.ReadOnly));
    }

    [Fact]
    public async Task SetAttributes_SetReadOnly_AppliesAttribute()
    {
        var filePath = Path.Combine(_testDir, "toreadonly.txt");
        File.WriteAllText(filePath, "");

        await _service.SetAttributesAsync(filePath, isReadOnly: true, isHidden: false);

        var attrs = File.GetAttributes(filePath);
        Assert.True(attrs.HasFlag(FileAttributes.ReadOnly));
        Assert.False(attrs.HasFlag(FileAttributes.Hidden));

        // Cleanup
        File.SetAttributes(filePath, FileAttributes.Normal);
    }

    [Fact]
    public async Task SetAttributes_ClearAll_RemovesAttributes()
    {
        var filePath = Path.Combine(_testDir, "clearattrs.txt");
        File.WriteAllText(filePath, "");
        File.SetAttributes(filePath, FileAttributes.Hidden | FileAttributes.ReadOnly);

        await _service.SetAttributesAsync(filePath, isReadOnly: false, isHidden: false);

        var attrs = File.GetAttributes(filePath);
        Assert.False(attrs.HasFlag(FileAttributes.Hidden));
        Assert.False(attrs.HasFlag(FileAttributes.ReadOnly));
    }

    [Fact]
    public async Task SetAttributes_NonExistentPath_ThrowsFileNotFound()
    {
        var path = Path.Combine(_testDir, "nope.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.SetAttributesAsync(path, false, false));
    }

    // --- Multiple items scenario ---

    [Fact]
    public async Task CalculateDirectorySize_MultipleDirectories_CanBeCalledSequentially()
    {
        var dir1 = Path.Combine(_testDir, "multi1");
        var dir2 = Path.Combine(_testDir, "multi2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);
        File.WriteAllBytes(Path.Combine(dir1, "a.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(dir2, "b.bin"), new byte[200]);

        var result1 = await _service.CalculateDirectorySizeAsync(dir1);
        var result2 = await _service.CalculateDirectorySizeAsync(dir2);

        Assert.Equal(100, result1.TotalSize);
        Assert.Equal(200, result2.TotalSize);
        Assert.Equal(300, result1.TotalSize + result2.TotalSize);
    }
}
