using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.FileSystem;
using Xunit;

namespace NexusExplorer.Tests;

public class PropertiesAndOpenWithTests : IDisposable
{
    private readonly string _testDir;
    private readonly FilePropertiesService _service;

    public PropertiesAndOpenWithTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"NexusPropsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new FilePropertiesService();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    // ================================================================
    // 1. Obtener metadata de archivo
    // ================================================================

    [Fact]
    public async Task GetProperties_File_ReturnsCorrectMetadata()
    {
        var filePath = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(filePath, "Hello World");

        var props = await _service.GetPropertiesAsync(filePath);

        Assert.Equal("test.txt", props.Name);
        Assert.Equal(filePath, props.Path);
        Assert.Equal(_testDir, props.DirectoryPath);
        Assert.False(props.IsDirectory);
        Assert.Equal("Text File", props.TypeDescription);
    }

    // ================================================================
    // 2. Obtener metadata de carpeta
    // ================================================================

    [Fact]
    public async Task GetProperties_Directory_ReturnsCorrectMetadata()
    {
        var dirPath = Path.Combine(_testDir, "SubFolder");
        Directory.CreateDirectory(dirPath);

        var props = await _service.GetPropertiesAsync(dirPath);

        Assert.Equal("SubFolder", props.Name);
        Assert.Equal(dirPath, props.Path);
        Assert.True(props.IsDirectory);
        Assert.Equal("Folder", props.TypeDescription);
    }

    // ================================================================
    // 3. Obtener tamaño
    // ================================================================

    [Fact]
    public async Task GetProperties_File_ReturnsSize()
    {
        var filePath = Path.Combine(_testDir, "sized.bin");
        File.WriteAllBytes(filePath, new byte[1024]);

        var props = await _service.GetPropertiesAsync(filePath);

        Assert.Equal(1024, props.Size);
    }

    // ================================================================
    // 4. Obtener fechas
    // ================================================================

    [Fact]
    public async Task GetProperties_File_ReturnsDates()
    {
        var filePath = Path.Combine(_testDir, "dated.txt");
        File.WriteAllText(filePath, "test");

        var props = await _service.GetPropertiesAsync(filePath);

        Assert.NotNull(props.Created);
        Assert.NotNull(props.Modified);
        Assert.NotNull(props.LastAccessed);
        Assert.True(props.Created <= DateTime.Now);
        Assert.True(props.Modified <= DateTime.Now);
    }

    // ================================================================
    // 5. Obtener atributos
    // ================================================================

    [Fact]
    public async Task GetProperties_ReadOnlyFile_DetectsAttribute()
    {
        var filePath = Path.Combine(_testDir, "readonly.txt");
        File.WriteAllText(filePath, "test");
        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        try
        {
            var props = await _service.GetPropertiesAsync(filePath);
            Assert.True(props.IsReadOnly);
        }
        finally
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task GetProperties_HiddenFile_DetectsAttribute()
    {
        var filePath = Path.Combine(_testDir, "hidden.txt");
        File.WriteAllText(filePath, "test");
        File.SetAttributes(filePath, FileAttributes.Hidden);

        var props = await _service.GetPropertiesAsync(filePath);
        Assert.True(props.IsHidden);
    }

    // ================================================================
    // 6. Archivo inexistente
    // ================================================================

    [Fact]
    public async Task GetProperties_NonExistent_ThrowsFileNotFound()
    {
        var path = Path.Combine(_testDir, "does_not_exist.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.GetPropertiesAsync(path));
    }

    // ================================================================
    // 7. Access denied (set attributes on non-existent)
    // ================================================================

    [Fact]
    public async Task SetAttributes_NonExistent_ThrowsFileNotFound()
    {
        var path = Path.Combine(_testDir, "nope.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.SetAttributesAsync(path, false, false));
    }

    // ================================================================
    // 8. Copy Path (logic test — the ViewModel exposes this)
    // ================================================================

    [Fact]
    public async Task GetProperties_PathMatchesInput()
    {
        var filePath = Path.Combine(_testDir, "pathtest.txt");
        File.WriteAllText(filePath, "test");

        var props = await _service.GetPropertiesAsync(filePath);

        // The path from properties should match the input path
        Assert.Equal(filePath, props.Path);
    }

    // ================================================================
    // 9. File with known association (type description)
    // ================================================================

    [Theory]
    [InlineData(".txt", "Text File")]
    [InlineData(".pdf", "PDF Document")]
    [InlineData(".png", "PNG Image")]
    [InlineData(".jpg", "JPEG Image")]
    [InlineData(".cs", "C# Source File")]
    [InlineData(".json", "JSON File")]
    [InlineData(".exe", "Executable")]
    [InlineData(".zip", "ZIP Archive")]
    public async Task GetProperties_FileType_ReturnsCorrectDescription(string ext, string expectedType)
    {
        var filePath = Path.Combine(_testDir, $"file{ext}");
        File.WriteAllText(filePath, "test");

        var props = await _service.GetPropertiesAsync(filePath);

        Assert.Equal(expectedType, props.TypeDescription);
    }

    // ================================================================
    // 10. File without extension
    // ================================================================

    [Fact]
    public async Task GetProperties_NoExtension_ReturnsGenericType()
    {
        var filePath = Path.Combine(_testDir, "README");
        File.WriteAllText(filePath, "test");

        var props = await _service.GetPropertiesAsync(filePath);

        Assert.Equal("File", props.TypeDescription);
    }

    // ================================================================
    // 11. Set attributes works
    // ================================================================

    [Fact]
    public async Task SetAttributes_ReadOnly_Applied()
    {
        var filePath = Path.Combine(_testDir, "setattr.txt");
        File.WriteAllText(filePath, "test");

        await _service.SetAttributesAsync(filePath, isReadOnly: true, isHidden: false);

        var attrs = File.GetAttributes(filePath);
        Assert.True(attrs.HasFlag(FileAttributes.ReadOnly));

        // Cleanup
        File.SetAttributes(filePath, FileAttributes.Normal);
    }

    [Fact]
    public async Task SetAttributes_Hidden_Applied()
    {
        var filePath = Path.Combine(_testDir, "sethidden.txt");
        File.WriteAllText(filePath, "test");

        await _service.SetAttributesAsync(filePath, isReadOnly: false, isHidden: true);

        var attrs = File.GetAttributes(filePath);
        Assert.True(attrs.HasFlag(FileAttributes.Hidden));
    }

    // ================================================================
    // 12. Directory size calculation
    // ================================================================

    [Fact]
    public async Task CalculateDirectorySize_ReturnsCorrectCounts()
    {
        var subDir = Path.Combine(_testDir, "CountTest");
        Directory.CreateDirectory(subDir);
        File.WriteAllBytes(Path.Combine(subDir, "a.txt"), new byte[100]);
        File.WriteAllBytes(Path.Combine(subDir, "b.txt"), new byte[200]);
        Directory.CreateDirectory(Path.Combine(subDir, "inner"));
        File.WriteAllBytes(Path.Combine(subDir, "inner", "c.txt"), new byte[300]);

        var result = await _service.CalculateDirectorySizeAsync(subDir);

        Assert.Equal(600, result.TotalSize);
        Assert.Equal(3, result.FileCount);
        Assert.Equal(1, result.FolderCount);
        Assert.True(result.WasCompleted);
    }

    // ================================================================
    // 13. Cancellation of directory size
    // ================================================================

    [Fact]
    public async Task CalculateDirectorySize_Cancellable()
    {
        var subDir = Path.Combine(_testDir, "CancelTest");
        Directory.CreateDirectory(subDir);
        for (int i = 0; i < 10; i++)
            File.WriteAllText(Path.Combine(subDir, $"file{i}.txt"), "x");

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Should throw either OperationCanceledException or TaskCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.CalculateDirectorySizeAsync(subDir, cancellationToken: cts.Token));
    }
}
