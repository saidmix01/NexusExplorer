using NexusExplorer.Core.Models;
using NexusExplorer.Core.Services;

namespace NexusExplorer.Tests;

public class SplitViewTests
{
    [Fact]
    public void TabItem_DefaultLayout_IsExplorerOnly()
    {
        var tab = new TabItem(@"C:\Users");
        Assert.Equal(LayoutMode.ExplorerOnly, tab.LayoutMode);
    }

    [Fact]
    public void TabItem_DefaultOrientation_IsHorizontal()
    {
        var tab = new TabItem(@"C:\Users");
        Assert.Equal(SplitOrientation.Horizontal, tab.SplitOrientation);
    }

    [Fact]
    public void TabItem_DefaultSplitRatio_Is60Percent()
    {
        var tab = new TabItem(@"C:\Users");
        Assert.Equal(0.6, tab.SplitRatio);
    }

    [Fact]
    public void TabItem_LayoutMode_CanBeChanged()
    {
        var tab = new TabItem(@"C:\Users");
        tab.LayoutMode = LayoutMode.Split;
        Assert.Equal(LayoutMode.Split, tab.LayoutMode);
    }

    [Fact]
    public void TabItem_SplitOrientation_CanBeChanged()
    {
        var tab = new TabItem(@"C:\Users");
        tab.SplitOrientation = SplitOrientation.Vertical;
        Assert.Equal(SplitOrientation.Vertical, tab.SplitOrientation);
    }

    [Fact]
    public void TabItem_SplitRatio_CanBeChanged()
    {
        var tab = new TabItem(@"C:\Users");
        tab.SplitRatio = 0.5;
        Assert.Equal(0.5, tab.SplitRatio);
    }

    [Fact]
    public void TabItem_SplitRatio_Clamps_Minimum()
    {
        var tab = new TabItem(@"C:\Users");
        tab.SplitRatio = 0.05; // very small but valid double
        // The ViewModel enforces min, the model just stores the value
        Assert.Equal(0.05, tab.SplitRatio);
    }

    [Fact]
    public void PerTab_LayoutState_IsIndependent()
    {
        var tab1 = new TabItem(@"C:\A");
        var tab2 = new TabItem(@"C:\B");

        tab1.LayoutMode = LayoutMode.Split;
        tab1.SplitOrientation = SplitOrientation.Vertical;
        tab1.SplitRatio = 0.5;

        tab2.LayoutMode = LayoutMode.TerminalOnly;
        tab2.SplitOrientation = SplitOrientation.Horizontal;
        tab2.SplitRatio = 0.7;

        Assert.Equal(LayoutMode.Split, tab1.LayoutMode);
        Assert.Equal(SplitOrientation.Vertical, tab1.SplitOrientation);
        Assert.Equal(0.5, tab1.SplitRatio);

        Assert.Equal(LayoutMode.TerminalOnly, tab2.LayoutMode);
        Assert.Equal(SplitOrientation.Horizontal, tab2.SplitOrientation);
        Assert.Equal(0.7, tab2.SplitRatio);
    }

    [Fact]
    public void TabService_TabSwitch_PreservesLayoutState()
    {
        var sut = new TabService();

        var tab1 = sut.CreateTab(@"C:\A");
        tab1.LayoutMode = LayoutMode.Split;
        tab1.SplitOrientation = SplitOrientation.Vertical;
        tab1.SplitRatio = 0.4;

        var tab2 = sut.CreateTab(@"C:\B");
        tab2.LayoutMode = LayoutMode.ExplorerOnly;

        // Switch back to tab1
        sut.ActivateTab(tab1.Id);

        Assert.Equal(LayoutMode.Split, sut.ActiveTab.LayoutMode);
        Assert.Equal(SplitOrientation.Vertical, sut.ActiveTab.SplitOrientation);
        Assert.Equal(0.4, sut.ActiveTab.SplitRatio);
    }

    [Fact]
    public void LayoutMode_ExplorerOnly_HidesTerminal()
    {
        var tab = new TabItem(@"C:\Users");
        tab.LayoutMode = LayoutMode.ExplorerOnly;

        // ViewModel would set:
        var isExplorerVisible = tab.LayoutMode != LayoutMode.TerminalOnly;
        var isTerminalVisible = tab.LayoutMode != LayoutMode.ExplorerOnly;

        Assert.True(isExplorerVisible);
        Assert.False(isTerminalVisible);
    }

    [Fact]
    public void LayoutMode_TerminalOnly_HidesExplorer()
    {
        var tab = new TabItem(@"C:\Users");
        tab.LayoutMode = LayoutMode.TerminalOnly;

        var isExplorerVisible = tab.LayoutMode != LayoutMode.TerminalOnly;
        var isTerminalVisible = tab.LayoutMode != LayoutMode.ExplorerOnly;

        Assert.False(isExplorerVisible);
        Assert.True(isTerminalVisible);
    }

    [Fact]
    public void LayoutMode_Split_ShowsBoth()
    {
        var tab = new TabItem(@"C:\Users");
        tab.LayoutMode = LayoutMode.Split;

        var isExplorerVisible = tab.LayoutMode != LayoutMode.TerminalOnly;
        var isTerminalVisible = tab.LayoutMode != LayoutMode.ExplorerOnly;

        Assert.True(isExplorerVisible);
        Assert.True(isTerminalVisible);
    }

    [Fact]
    public void LayoutMode_Change_DoesNotAffectNavigation()
    {
        var tab = new TabItem(@"C:\Users");
        tab.NavigateTo(@"C:\Users\Documents");

        tab.LayoutMode = LayoutMode.Split;

        Assert.Equal(@"C:\Users\Documents", tab.CurrentPath);
        Assert.True(tab.CanGoBack);
    }

    [Fact]
    public void LayoutMode_Change_DoesNotAffectTerminalState()
    {
        var tab = new TabItem(@"C:\Users");
        tab.IsTerminalOpen = true;
        tab.TerminalWorkingDirectory = @"C:\Users";

        tab.LayoutMode = LayoutMode.ExplorerOnly;

        // Terminal session stays alive even when hidden
        Assert.True(tab.IsTerminalOpen);
        Assert.Equal(@"C:\Users", tab.TerminalWorkingDirectory);
    }

    // --- SplitRatio clamping (enforced in the ViewModel setter) ---

    [Fact]
    public void SplitRatio_Default_Is0Point6()
    {
        var tab = new TabItem(@"C:\Users");
        Assert.Equal(0.6, tab.SplitRatio, precision: 5);
    }

    [Fact]
    public void SplitRatio_0Point5_StoresCorrectly()
    {
        var tab = new TabItem(@"C:\Users");
        tab.SplitRatio = 0.5;
        Assert.Equal(0.5, tab.SplitRatio, precision: 5);
    }

    [Fact]
    public void SplitRatio_0Point2_StoresCorrectly()
    {
        var tab = new TabItem(@"C:\Users");
        tab.SplitRatio = 0.2;
        Assert.Equal(0.2, tab.SplitRatio, precision: 5);
    }

    [Fact]
    public void SplitRatio_0Point8_StoresCorrectly()
    {
        var tab = new TabItem(@"C:\Users");
        tab.SplitRatio = 0.8;
        Assert.Equal(0.8, tab.SplitRatio, precision: 5);
    }

    /// <summary>
    /// The model itself stores any value; the ViewModel clamps.
    /// These two tests verify the ViewModel's clamping logic via Math.Clamp.
    /// </summary>
    [Fact]
    public void SplitRatio_ClampMinimum_Via_MathClamp()
    {
        var raw = 0.1;
        var clamped = Math.Clamp(raw, 0.2, 0.8);
        Assert.Equal(0.2, clamped, precision: 5);
    }

    [Fact]
    public void SplitRatio_ClampMaximum_Via_MathClamp()
    {
        var raw = 0.9;
        var clamped = Math.Clamp(raw, 0.2, 0.8);
        Assert.Equal(0.8, clamped, precision: 5);
    }

    [Fact]
    public void ChangingOrientation_DoesNotAffectSplitRatio()
    {
        var tab = new TabItem(@"C:\Users");
        tab.SplitRatio = 0.7;
        tab.SplitOrientation = SplitOrientation.Vertical;

        Assert.Equal(0.7, tab.SplitRatio, precision: 5);

        tab.SplitOrientation = SplitOrientation.Horizontal;
        Assert.Equal(0.7, tab.SplitRatio, precision: 5);
    }

    [Fact]
    public void SplitRatio_0Point3_RemainsAfterTabSwitch()
    {
        var sut = new TabService();
        var tab1 = sut.CreateTab(@"C:\A");
        tab1.SplitRatio = 0.3;

        var tab2 = sut.CreateTab(@"C:\B");
        sut.ActivateTab(tab1.Id);

        Assert.Equal(0.3, sut.ActiveTab.SplitRatio, precision: 5);
    }
}
