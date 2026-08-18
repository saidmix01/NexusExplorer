using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.FileSystem;

namespace NexusExplorer.Tests;

public class FileSystemMappingTests
{
    [Fact]
    public void MapDirectory_SetsCorrectProperties()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"NexusTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var dirInfo = new DirectoryInfo(tempDir);
            var result = FileSystemService.MapDirectory(dirInfo);

            Assert.Equal(dirInfo.Name, result.Name);
            Assert.Equal(dirInfo.FullName, result.Path);
            Assert.Equal(FileSystemItemType.Directory, result.Type);
            Assert.Null(result.Size);
            Assert.Null(result.Extension);
            Assert.NotNull(result.LastModified);
            Assert.NotNull(result.Created);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public void MapFile_SetsCorrectProperties()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, "Hello, World!");
            var fileInfo = new FileInfo(tempFile);
            var result = FileSystemService.MapFile(fileInfo);

            Assert.Equal(fileInfo.Name, result.Name);
            Assert.Equal(fileInfo.FullName, result.Path);
            Assert.Equal(FileSystemItemType.File, result.Type);
            Assert.Equal(fileInfo.Length, result.Size);
            Assert.Equal(fileInfo.Extension, result.Extension);
            Assert.NotNull(result.LastModified);
            Assert.NotNull(result.Created);
            Assert.False(result.IsHidden);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void MapFile_ZeroLengthFile_HasZeroSize()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            var fileInfo = new FileInfo(tempFile);
            var result = FileSystemService.MapFile(fileInfo);

            Assert.Equal(0L, result.Size);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void MapFile_CapturesExtension()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"NexusTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.txt");
        File.WriteAllText(tempFile, "content");

        try
        {
            var fileInfo = new FileInfo(tempFile);
            var result = FileSystemService.MapFile(fileInfo);

            Assert.Equal(".txt", result.Extension);
        }
        finally
        {
            File.Delete(tempFile);
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public void MapDirectory_IsNotFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"NexusTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var dirInfo = new DirectoryInfo(tempDir);
            var result = FileSystemService.MapDirectory(dirInfo);

            Assert.Equal(FileSystemItemType.Directory, result.Type);
            Assert.NotEqual(FileSystemItemType.File, result.Type);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }
}
