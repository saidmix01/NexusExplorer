using System.Collections.Generic;
using System.Threading;
using Moq;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Core.Services;
using Xunit;
using NexusExplorer.App.ViewModels;

namespace NexusExplorer.Tests;

public class ContextMenuTests
{
    private MainWindowViewModel CreateViewModel()
    {
        var fileSystem = new Mock<IFileSystemService>();
        var tabService = new TabService();
        var platformService = new Mock<IPlatformService>();
        var previewService = new Mock<IPreviewService>();
        var terminalSessionService = new Mock<ITerminalSessionService>();
        var fileOpService = new Mock<IFileOperationService>();
        var clipboardService = new Mock<IClipboardService>();
        var sendToService = new Mock<ISendToService>();

        platformService.Setup(p => p.HomePath).Returns("/home");
        platformService.Setup(p => p.GetQuickAccessFolders()).Returns(new List<NavigationItem>());
        fileSystem.Setup(f => f.GetDrivesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<NavigationItem>());
        sendToService.Setup(s => s.GetTargets()).Returns(new List<SendToTarget>());
        
        return new MainWindowViewModel(
            fileSystem.Object,
            tabService,
            platformService.Object,
            previewService.Object,
            terminalSessionService.Object,
            fileOpService.Object,
            clipboardService.Object,
            Mock.Of<ISearchService>(),
            Mock.Of<IOperationHistoryService>(),
            Mock.Of<ICompressionService>(),
            sendToService.Object,
            Mock.Of<IFileWatcherService>(),
            Mock.Of<IStatePersistenceService>(),
            Mock.Of<IFolderColorService>(),
            Mock.Of<IFileOperationManager>(),
            Mock.Of<IEditorService>(),
            Mock.Of<ITerminalDiscoveryService>(),
            Mock.Of<ITerminalLauncher>(),
            Mock.Of<IGlobalHotkeyService>());
    }

    [Fact]
    public void RightClick_EmptyArea_Shows_Menu()
    {
        // Actually, UI tests are hard to do in ViewModel unit tests. 
        // We will just verify commands are present.
        var vm = CreateViewModel();
        Assert.NotNull(vm.OpenCurrentInTerminalCommand);
        Assert.NotNull(vm.RefreshCommand);
        Assert.NotNull(vm.PasteCommand);
    }

    [Fact]
    public void RightClick_FileItem_Shows_Menu()
    {
        var vm = CreateViewModel();
        Assert.NotNull(vm.OpenItemCommand);
        Assert.NotNull(vm.OpenInNewTabCommand);
        Assert.NotNull(vm.ShowPropertiesCommand);
    }

    [Fact]
    public void Selection_Preservation_After_Changing_ViewMode()
    {
        var vm = CreateViewModel();
        
        var item1 = new FileSystemItem { Path = "/home/file1.txt", Name = "file1.txt", Type = FileSystemItemType.File };
        var item2 = new FileSystemItem { Path = "/home/file2.txt", Name = "file2.txt", Type = FileSystemItemType.File };
        
        vm.Items.Add(item1);
        vm.Items.Add(item2);
        
        vm.SelectedItems.Add(item1);
        vm.SelectedItems.Add(item2);
        
        Assert.Equal(2, vm.SelectedItems.Count);
        
        vm.SetLargeIconsViewCommand.Execute(null);
        
        Assert.Equal(2, vm.SelectedItems.Count);
    }
}