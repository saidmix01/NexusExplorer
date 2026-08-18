using System.Collections.Generic;
using System.Threading;
using Moq;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Core.Services;
using Xunit;
using NexusExplorer.App.ViewModels;

namespace NexusExplorer.Tests;

public class ViewModeTests
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
    public void ViewMode_Default_IsDetails()
    {
        var vm = CreateViewModel();
        Assert.Equal(ExplorerViewMode.Details, vm.ViewMode);
    }

    [Fact]
    public void Changing_ViewMode_Updates_ActiveTab()
    {
        var vm = CreateViewModel();
        vm.SetLargeIconsViewCommand.Execute(null);
        Assert.Equal(ExplorerViewMode.LargeIcons, vm.ViewMode);
        Assert.Equal(ExplorerViewMode.LargeIcons, vm.Tabs[0].ViewMode);
    }

    [Fact]
    public void ViewMode_Independence_Between_Tabs()
    {
        var vm = CreateViewModel();
        
        // Tab 1 is Details (Default)
        Assert.Equal(ExplorerViewMode.Details, vm.ViewMode);
        
        // Create Tab 2
        vm.NewTabCommand.Execute(null);
        vm.ActivateTabCommand.Execute(vm.Tabs[1].Id);
        
        // Change Tab 2 to LargeIcons
        vm.SetLargeIconsViewCommand.Execute(null);
        Assert.Equal(ExplorerViewMode.LargeIcons, vm.ViewMode);
        
        // Switch back to Tab 1
        vm.ActivateTabCommand.Execute(vm.Tabs[0].Id);
        Assert.Equal(ExplorerViewMode.Details, vm.ViewMode);
    }
}