using Moq;
using NexusExplorer.App.ViewModels;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Core.Services;

namespace NexusExplorer.Tests;

public class CopyPathTests
{
    private MainWindowViewModel CreateViewModel(Mock<ISendToService>? sendToService = null)
    {
        var fileSystem = new Mock<IFileSystemService>();
        var tabService = new TabService();
        var platformService = new Mock<IPlatformService>();
        var terminalSessionService = new Mock<ITerminalSessionService>();
        var fileOpService = new Mock<IFileOperationService>();
        var clipboardService = new Mock<IClipboardService>();

        platformService.Setup(p => p.HomePath).Returns("/home");
        platformService.Setup(p => p.GetQuickAccessFolders()).Returns(new List<NavigationItem>());
        fileSystem.Setup(f => f.GetDrivesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<NavigationItem>());

        var sts = sendToService ?? new Mock<ISendToService>();
        sts.Setup(s => s.GetTargets()).Returns(new List<SendToTarget>());

        return new MainWindowViewModel(
            fileSystem.Object,
            tabService,
            platformService.Object,
            Mock.Of<IPreviewService>(),
            terminalSessionService.Object,
            fileOpService.Object,
            clipboardService.Object,
            Mock.Of<ISearchService>(),
            Mock.Of<IOperationHistoryService>(),
            Mock.Of<ICompressionService>(),
            sts.Object,
            Mock.Of<IFileWatcherService>(),
            Mock.Of<IStatePersistenceService>(),
            Mock.Of<IFolderColorService>(),
            Mock.Of<IFileOperationManager>(),
            Mock.Of<IEditorService>());
    }

    [Fact]
    public void CopyPath_SingleFile_CopiesAbsolutePath()
    {
        var vm = CreateViewModel();
        string? copiedText = null;
        vm.CopyTextToClipboardRequested += text => copiedText = text;

        var item = new FileSystemItem
        {
            Path = @"C:\Users\Said\Documents\archivo.txt",
            Name = "archivo.txt",
            Type = FileSystemItemType.File
        };
        vm.Items.Add(item);
        vm.SelectedItems.Add(item);

        vm.CopyPathCommand.Execute(null);

        Assert.Equal(@"C:\Users\Said\Documents\archivo.txt", copiedText);
        Assert.Equal("Path copied", vm.StatusText);
    }

    [Fact]
    public void CopyPath_SingleFolder_CopiesAbsolutePath()
    {
        var vm = CreateViewModel();
        string? copiedText = null;
        vm.CopyTextToClipboardRequested += text => copiedText = text;

        var item = new FileSystemItem
        {
            Path = @"C:\Users\Said\Documents\Proyecto",
            Name = "Proyecto",
            Type = FileSystemItemType.Directory
        };
        vm.Items.Add(item);
        vm.SelectedItems.Add(item);

        vm.CopyPathCommand.Execute(null);

        Assert.Equal(@"C:\Users\Said\Documents\Proyecto", copiedText);
    }

    [Fact]
    public void CopyPath_MultipleFiles_CopiesOnePerLine()
    {
        var vm = CreateViewModel();
        string? copiedText = null;
        vm.CopyTextToClipboardRequested += text => copiedText = text;

        var item1 = new FileSystemItem
        {
            Path = @"C:\A.txt",
            Name = "A.txt",
            Type = FileSystemItemType.File
        };
        var item2 = new FileSystemItem
        {
            Path = @"C:\B.txt",
            Name = "B.txt",
            Type = FileSystemItemType.File
        };
        var item3 = new FileSystemItem
        {
            Path = @"C:\C.txt",
            Name = "C.txt",
            Type = FileSystemItemType.File
        };

        vm.Items.Add(item1);
        vm.Items.Add(item2);
        vm.Items.Add(item3);
        vm.SelectedItems.Add(item1);
        vm.SelectedItems.Add(item2);
        vm.SelectedItems.Add(item3);

        vm.CopyPathCommand.Execute(null);

        var expected = @"C:\A.txt" + Environment.NewLine + @"C:\B.txt" + Environment.NewLine + @"C:\C.txt";
        Assert.Equal(expected, copiedText);
        Assert.Equal("3 paths copied", vm.StatusText);
    }

    [Fact]
    public void CopyPath_PathWithSpaces_CopiesCorrectly()
    {
        var vm = CreateViewModel();
        string? copiedText = null;
        vm.CopyTextToClipboardRequested += text => copiedText = text;

        var item = new FileSystemItem
        {
            Path = @"C:\Users\Said\My Documents\My File.txt",
            Name = "My File.txt",
            Type = FileSystemItemType.File
        };
        vm.Items.Add(item);
        vm.SelectedItems.Add(item);

        vm.CopyPathCommand.Execute(null);

        Assert.Equal(@"C:\Users\Said\My Documents\My File.txt", copiedText);
    }

    [Fact]
    public void CopyPath_NoSelection_DoesNothing()
    {
        var vm = CreateViewModel();
        string? copiedText = null;
        vm.CopyTextToClipboardRequested += text => copiedText = text;

        vm.CopyPathCommand.Execute(null);

        Assert.Null(copiedText);
    }
}
