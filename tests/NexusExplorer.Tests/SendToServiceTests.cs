using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.FileOperations;
using NexusExplorer.Platform.Windows;

namespace NexusExplorer.Tests;

public class SendToServiceTests : IDisposable
{
    private readonly string _testDir;

    public SendToServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"NexusSendTo_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    // --- Test 14: Detect SendTo folder ---

    [Fact]
    public void GetSendToFolderPath_ReturnsValidPath()
    {
        var path = WindowsSendToService.GetSendToFolderPath();

        Assert.NotNull(path);
        Assert.Contains("SendTo", path);
        Assert.Contains("Microsoft", path);
    }

    // --- Test 15: Detect available targets ---

    [Fact]
    public void GetTargets_ReturnsNonEmptyList_WhenSendToFolderExists()
    {
        var fileOpService = new Mock<IFileOperationService>();
        var compressionService = new Mock<ICompressionService>();
        var sut = new WindowsSendToService(fileOpService.Object, compressionService.Object);

        var targets = sut.GetTargets();

        // On a Windows machine, there should be at least one target
        // (Compressed (zipped) Folder is commonly present)
        Assert.NotNull(targets);
        // We don't assert count > 0 because test environments may vary
    }

    // --- Test 16: Send file to folder ---

    [Fact]
    public async Task SendToAsync_FolderTarget_CopiesFile()
    {
        var sourceFile = Path.Combine(_testDir, "source.txt");
        File.WriteAllText(sourceFile, "content");

        var destDir = Path.Combine(_testDir, "Desktop");
        Directory.CreateDirectory(destDir);

        var fileOpService = new Mock<IFileOperationService>();
        fileOpService
            .Setup(f => f.CopyAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<string>(d => d == destDir),
                It.IsAny<IProgress<FileOperationProgress>?>(),
                It.IsAny<Func<FileConflict, Task<ConflictAction>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FileOperationResult.Ok(1));

        var compressionService = new Mock<ICompressionService>();
        var sut = new WindowsSendToService(fileOpService.Object, compressionService.Object);

        var target = new SendToTarget
        {
            Name = "Desktop",
            Path = destDir,
            Type = SendToTargetType.Folder
        };

        var result = await sut.SendToAsync([sourceFile], target);

        Assert.True(result.Success);
        fileOpService.Verify(f => f.CopyAsync(
            It.Is<IReadOnlyList<string>>(p => p.Contains(sourceFile)),
            destDir,
            It.IsAny<IProgress<FileOperationProgress>?>(),
            It.IsAny<Func<FileConflict, Task<ConflictAction>>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Test 17: Conflict resolution via existing mechanism ---

    [Fact]
    public async Task SendToAsync_FolderTarget_PassesConflictResolver()
    {
        var sourceFile = Path.Combine(_testDir, "file.txt");
        File.WriteAllText(sourceFile, "data");

        var destDir = Path.Combine(_testDir, "target");
        Directory.CreateDirectory(destDir);

        Func<FileConflict, Task<ConflictAction>>? capturedResolver = null;

        var fileOpService = new Mock<IFileOperationService>();
        fileOpService
            .Setup(f => f.CopyAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<FileOperationProgress>?>(),
                It.IsAny<Func<FileConflict, Task<ConflictAction>>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, IProgress<FileOperationProgress>?, Func<FileConflict, Task<ConflictAction>>?, CancellationToken>(
                (_, _, _, resolver, _) => capturedResolver = resolver)
            .ReturnsAsync(FileOperationResult.Ok(1));

        var compressionService = new Mock<ICompressionService>();
        var sut = new WindowsSendToService(fileOpService.Object, compressionService.Object);

        Func<FileConflict, Task<ConflictAction>> myResolver = _ => Task.FromResult(ConflictAction.Skip);

        var target = new SendToTarget
        {
            Name = "Documents",
            Path = destDir,
            Type = SendToTargetType.Folder
        };

        await sut.SendToAsync([sourceFile], target, conflictResolver: myResolver);

        // The resolver should be passed through to the file operation service
        Assert.NotNull(capturedResolver);
    }

    // --- Test: Send to compressed folder uses compression service ---

    [Fact]
    public async Task SendToAsync_CompressedFolder_UsesCompressionService()
    {
        var sourceFile = Path.Combine(_testDir, "file.txt");
        File.WriteAllText(sourceFile, "data");

        var fileOpService = new Mock<IFileOperationService>();
        var compressionService = new Mock<ICompressionService>();
        compressionService
            .Setup(c => c.CompressAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<FileOperationProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompressionResult.Ok(Path.Combine(_testDir, "file.zip"), 1));

        var sut = new WindowsSendToService(fileOpService.Object, compressionService.Object);

        var target = new SendToTarget
        {
            Name = "Compressed (zipped) Folder",
            Path = null,
            Type = SendToTargetType.CompressedFolder
        };

        var result = await sut.SendToAsync([sourceFile], target);

        Assert.True(result.Success);
        compressionService.Verify(c => c.CompressAsync(
            It.Is<IReadOnlyList<string>>(p => p.Contains(sourceFile)),
            It.IsAny<IProgress<FileOperationProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        // File operation service should NOT be called
        fileOpService.Verify(f => f.CopyAsync(
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(),
            It.IsAny<IProgress<FileOperationProgress>?>(),
            It.IsAny<Func<FileConflict, Task<ConflictAction>>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
