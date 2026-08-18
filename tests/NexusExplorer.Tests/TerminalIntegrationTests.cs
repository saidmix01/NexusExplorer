using NexusExplorer.Core.Models;
using NexusExplorer.Core.Services;

namespace NexusExplorer.Tests;

/// <summary>
/// Tests for terminal state management per tab: lazy creation, tab switching,
/// disposal, working directory, and independent terminal state.
/// These tests verify the TabItem and TabService behavior without launching real processes.
/// </summary>
public class TerminalIntegrationTests
{
    // --- Lazy creation ---

    [Fact]
    public void TabItem_InitialTerminalState_IsNull()
    {
        var tab = new TabItem(@"C:\Users");

        Assert.Null(tab.TerminalSession);
        Assert.False(tab.IsTerminalOpen);
        Assert.Null(tab.TerminalWorkingDirectory);
        Assert.Null(tab.TerminalError);
    }

    [Fact]
    public void TabItem_TerminalSessionNotCreatedOnConstruction()
    {
        var tab = new TabItem(@"C:\Users\Projects");

        // Terminal is not created when tab is created — it's lazy
        Assert.Null(tab.TerminalSession);
        Assert.False(tab.IsTerminalOpen);
    }

    // --- Per-tab state ---

    [Fact]
    public void TabItem_TerminalState_IndependentPerTab()
    {
        var tab1 = new TabItem(@"C:\A");
        var tab2 = new TabItem(@"C:\B");

        tab1.IsTerminalOpen = true;
        tab1.TerminalWorkingDirectory = @"C:\A\src";

        tab2.IsTerminalOpen = false;
        tab2.TerminalWorkingDirectory = null;

        Assert.True(tab1.IsTerminalOpen);
        Assert.Equal(@"C:\A\src", tab1.TerminalWorkingDirectory);
        Assert.False(tab2.IsTerminalOpen);
        Assert.Null(tab2.TerminalWorkingDirectory);
    }

    [Fact]
    public void TabItem_TerminalError_IndependentPerTab()
    {
        var tab1 = new TabItem(@"C:\A");
        var tab2 = new TabItem(@"C:\B");

        tab1.TerminalError = "PowerShell not found";
        tab2.TerminalError = null;

        Assert.Equal("PowerShell not found", tab1.TerminalError);
        Assert.Null(tab2.TerminalError);
    }

    // --- Tab switching ---

    [Fact]
    public void TabService_TabSwitch_PreservesTerminalState()
    {
        var sut = new TabService();

        var tab1 = sut.CreateTab(@"C:\A");
        tab1.IsTerminalOpen = true;
        tab1.TerminalWorkingDirectory = @"C:\A";

        var tab2 = sut.CreateTab(@"C:\B");
        tab2.IsTerminalOpen = false;

        // Switch back to tab1
        sut.ActivateTab(tab1.Id);

        Assert.True(tab1.IsTerminalOpen);
        Assert.Equal(@"C:\A", tab1.TerminalWorkingDirectory);
    }

    [Fact]
    public void TabService_TabSwitch_EachTabHasOwnTerminalState()
    {
        var sut = new TabService();

        var tab1 = sut.CreateTab(@"C:\A");
        tab1.IsTerminalOpen = true;
        tab1.TerminalWorkingDirectory = @"C:\A\deep\path";

        var tab2 = sut.CreateTab(@"C:\B");
        tab2.IsTerminalOpen = true;
        tab2.TerminalWorkingDirectory = @"C:\B\other";

        // Active tab is tab2
        Assert.Equal(tab2.Id, sut.ActiveTab.Id);
        Assert.Equal(@"C:\B\other", sut.ActiveTab.TerminalWorkingDirectory);

        // Switch to tab1
        sut.ActivateTab(tab1.Id);
        Assert.Equal(@"C:\A\deep\path", sut.ActiveTab.TerminalWorkingDirectory);
    }

    // --- Terminal disposal ---

    [Fact]
    public void TabItem_DisposeTerminal_ClearsState()
    {
        var tab = new TabItem(@"C:\Users");
        tab.IsTerminalOpen = true;
        tab.TerminalWorkingDirectory = @"C:\Users";
        tab.TerminalError = "some error";

        tab.DisposeTerminal();

        Assert.Null(tab.TerminalSession);
        Assert.False(tab.IsTerminalOpen);
        Assert.Null(tab.TerminalWorkingDirectory);
        Assert.Null(tab.TerminalError);
    }

    [Fact]
    public void TabItem_DisposeTerminal_WhenNoSession_DoesNotThrow()
    {
        var tab = new TabItem(@"C:\Users");

        // Should not throw
        tab.DisposeTerminal();

        Assert.Null(tab.TerminalSession);
    }

    // --- Initial working directory ---

    [Fact]
    public void TabItem_TerminalWorkingDirectory_MatchesCurrentPath_WhenSet()
    {
        var tab = new TabItem(@"C:\Users\Projects\MyApp");
        tab.IsTerminalOpen = true;
        tab.TerminalWorkingDirectory = tab.CurrentPath;

        Assert.Equal(@"C:\Users\Projects\MyApp", tab.TerminalWorkingDirectory);
    }

    [Fact]
    public void TabItem_Navigation_DoesNotAutoUpdateTerminalWorkingDirectory()
    {
        var tab = new TabItem(@"C:\Users");
        tab.IsTerminalOpen = true;
        tab.TerminalWorkingDirectory = @"C:\Users";

        // Navigate explorer
        tab.NavigateTo(@"C:\Users\Documents");

        // Terminal working dir is NOT auto-updated (that's the ViewModel's job)
        Assert.Equal(@"C:\Users", tab.TerminalWorkingDirectory);
        Assert.Equal(@"C:\Users\Documents", tab.CurrentPath);
    }

    // --- Tab close disposes terminal ---

    [Fact]
    public void TabService_CloseTab_ShouldTriggerDisposal()
    {
        var sut = new TabService();

        var tab1 = sut.CreateTab(@"C:\A");
        tab1.IsTerminalOpen = true;
        tab1.TerminalWorkingDirectory = @"C:\A";

        sut.CreateTab(@"C:\B"); // need 2 tabs to close one

        // Simulate what MainWindowViewModel does before close
        tab1.DisposeTerminal();
        sut.CloseTab(tab1.Id);

        // Tab is gone
        Assert.DoesNotContain(tab1, sut.Tabs);
    }

    // --- Explorer navigation synchronization ---

    [Fact]
    public void TabItem_ExplorerNavigationChangesPath_TerminalCanSync()
    {
        var tab = new TabItem(@"C:\Users");
        tab.IsTerminalOpen = true;
        tab.TerminalWorkingDirectory = @"C:\Users";

        // Explorer navigates
        tab.NavigateTo(@"C:\Projects");

        // The ViewModel would sync terminal — here we verify the path changed
        Assert.Equal(@"C:\Projects", tab.CurrentPath);

        // Simulate sync
        tab.TerminalWorkingDirectory = tab.CurrentPath;
        Assert.Equal(@"C:\Projects", tab.TerminalWorkingDirectory);
    }

    [Fact]
    public void TabItem_MultipleNavigations_TerminalCanSyncToLatest()
    {
        var tab = new TabItem(@"C:\Users");
        tab.IsTerminalOpen = true;
        tab.TerminalWorkingDirectory = @"C:\Users";

        tab.NavigateTo(@"C:\A");
        tab.NavigateTo(@"C:\B");
        tab.NavigateTo(@"C:\C");

        // Sync terminal to latest
        tab.TerminalWorkingDirectory = tab.CurrentPath;
        Assert.Equal(@"C:\C", tab.TerminalWorkingDirectory);
    }
}
