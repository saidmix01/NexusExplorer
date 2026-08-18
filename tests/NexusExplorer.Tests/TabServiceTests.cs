using NexusExplorer.Core.Services;

namespace NexusExplorer.Tests;

public class TabServiceTests
{
    private readonly TabService _sut = new();

    [Fact]
    public void CreateTab_AddsTabAndActivatesIt()
    {
        var tab = _sut.CreateTab(@"C:\Users");

        Assert.Single(_sut.Tabs);
        Assert.Equal(tab.Id, _sut.ActiveTab.Id);
        Assert.True(tab.IsActive);
    }

    [Fact]
    public void CreateTab_WithTitle_SetsTitle()
    {
        var tab = _sut.CreateTab(@"C:\Users", "My Tab");

        Assert.Equal("My Tab", tab.Title);
    }

    [Fact]
    public void CreateTab_WithoutTitle_UsesFolderName()
    {
        var tab = _sut.CreateTab(@"C:\Users\Documents");

        Assert.Equal("Documents", tab.Title);
    }

    [Fact]
    public void CreateTab_SetsPath()
    {
        var tab = _sut.CreateTab(@"C:\Users\Documents");

        Assert.Equal(@"C:\Users\Documents", tab.CurrentPath);
    }

    [Fact]
    public void CreateTab_DeactivatesPreviousTab()
    {
        var tab1 = _sut.CreateTab(@"C:\A");
        var tab2 = _sut.CreateTab(@"C:\B");

        Assert.False(tab1.IsActive);
        Assert.True(tab2.IsActive);
    }

    [Fact]
    public void CreateTab_FiresTabsChangedEvent()
    {
        var fired = false;
        _sut.TabsChanged += (_, _) => fired = true;

        _sut.CreateTab(@"C:\Users");

        Assert.True(fired);
    }

    [Fact]
    public void CreateTab_FiresActiveTabChangedEvent()
    {
        _sut.CreateTab(@"C:\A"); // initial tab
        Core.Models.TabItem? changedTab = null;
        _sut.ActiveTabChanged += (_, tab) => changedTab = tab;

        var tab2 = _sut.CreateTab(@"C:\B");

        Assert.NotNull(changedTab);
        Assert.Equal(tab2.Id, changedTab.Id);
    }

    [Fact]
    public void CloseTab_RemovesTab()
    {
        var tab1 = _sut.CreateTab(@"C:\A");
        var tab2 = _sut.CreateTab(@"C:\B");

        _sut.CloseTab(tab1.Id);

        Assert.Single(_sut.Tabs);
        Assert.Equal(tab2.Id, _sut.Tabs[0].Id);
    }

    [Fact]
    public void CloseTab_LastTab_DoesNotRemove()
    {
        var tab = _sut.CreateTab(@"C:\Users");

        _sut.CloseTab(tab.Id);

        Assert.Single(_sut.Tabs);
        Assert.Equal(tab.Id, _sut.ActiveTab.Id);
    }

    [Fact]
    public void CloseTab_ActiveTab_ActivatesAdjacentTab()
    {
        _sut.CreateTab(@"C:\A");
        var tab2 = _sut.CreateTab(@"C:\B");
        var tab3 = _sut.CreateTab(@"C:\C");

        // tab3 is active, close it — should activate tab2 (left neighbor)
        _sut.CloseTab(tab3.Id);

        Assert.Equal(tab2.Id, _sut.ActiveTab.Id);
        Assert.True(tab2.IsActive);
    }

    [Fact]
    public void CloseTab_FirstActiveTab_ActivatesNext()
    {
        var tab1 = _sut.CreateTab(@"C:\A");
        var tab2 = _sut.CreateTab(@"C:\B");
        _sut.CreateTab(@"C:\C");

        _sut.ActivateTab(tab1.Id);
        _sut.CloseTab(tab1.Id);

        // Should activate the tab that was at index 1 (now at index 0 = tab2)
        Assert.Equal(tab2.Id, _sut.ActiveTab.Id);
    }

    [Fact]
    public void CloseTab_InactiveTab_DoesNotChangeActiveTab()
    {
        var tab1 = _sut.CreateTab(@"C:\A");
        _sut.CreateTab(@"C:\B");
        var tab3 = _sut.CreateTab(@"C:\C");

        // tab3 is active, close tab1
        _sut.CloseTab(tab1.Id);

        Assert.Equal(tab3.Id, _sut.ActiveTab.Id);
    }

    [Fact]
    public void ActivateTab_SwitchesActiveTab()
    {
        var tab1 = _sut.CreateTab(@"C:\A");
        _sut.CreateTab(@"C:\B");

        _sut.ActivateTab(tab1.Id);

        Assert.Equal(tab1.Id, _sut.ActiveTab.Id);
        Assert.True(tab1.IsActive);
    }

    [Fact]
    public void ActivateTab_DeactivatesPrevious()
    {
        _sut.CreateTab(@"C:\A");
        var tab2 = _sut.CreateTab(@"C:\B");

        _sut.ActivateTab(_sut.Tabs[0].Id);

        Assert.False(tab2.IsActive);
    }

    [Fact]
    public void ActivateTab_SameTab_DoesNotFireEvent()
    {
        var tab = _sut.CreateTab(@"C:\A");
        var fired = false;
        _sut.ActiveTabChanged += (_, _) => fired = true;

        _sut.ActivateTab(tab.Id);

        Assert.False(fired);
    }

    [Fact]
    public void ActivateNextTab_WrapsAround()
    {
        var tab1 = _sut.CreateTab(@"C:\A");
        _sut.CreateTab(@"C:\B");
        var tab3 = _sut.CreateTab(@"C:\C");

        // tab3 is active (index 2), next wraps to tab1 (index 0)
        _sut.ActivateNextTab();

        Assert.Equal(tab1.Id, _sut.ActiveTab.Id);
    }

    [Fact]
    public void ActivatePreviousTab_WrapsAround()
    {
        var tab1 = _sut.CreateTab(@"C:\A");
        _sut.CreateTab(@"C:\B");
        _sut.CreateTab(@"C:\C");

        // Activate first tab, then go previous — should wrap to last
        _sut.ActivateTab(tab1.Id);
        _sut.ActivatePreviousTab();

        Assert.Equal(_sut.Tabs[2].Id, _sut.ActiveTab.Id);
    }

    [Fact]
    public void ActivateNextTab_SingleTab_DoesNothing()
    {
        var tab = _sut.CreateTab(@"C:\A");

        _sut.ActivateNextTab();

        Assert.Equal(tab.Id, _sut.ActiveTab.Id);
    }

    [Fact]
    public void IndependentNavigation_TabsHaveOwnHistory()
    {
        var tab1 = _sut.CreateTab(@"C:\A");
        tab1.NavigateTo(@"C:\B");
        tab1.NavigateTo(@"C:\C");

        var tab2 = _sut.CreateTab(@"C:\X");
        tab2.NavigateTo(@"C:\Y");

        // Tab1 should have its own history
        Assert.True(tab1.CanGoBack);
        Assert.Equal(@"C:\C", tab1.CurrentPath);

        // Tab2 should have its own history
        Assert.True(tab2.CanGoBack);
        Assert.Equal(@"C:\Y", tab2.CurrentPath);

        // Going back on tab1 doesn't affect tab2
        tab1.GoBack();
        Assert.Equal(@"C:\B", tab1.CurrentPath);
        Assert.Equal(@"C:\Y", tab2.CurrentPath);
    }

    [Fact]
    public void TabNavigation_BackForward_Independent()
    {
        var tab1 = _sut.CreateTab(@"C:\A");
        tab1.NavigateTo(@"C:\B");

        var tab2 = _sut.CreateTab(@"C:\X");

        // Tab1 can go back, tab2 cannot
        Assert.True(tab1.CanGoBack);
        Assert.False(tab2.CanGoBack);

        // Go back on tab1
        tab1.GoBack();
        Assert.Equal(@"C:\A", tab1.CurrentPath);
        Assert.True(tab1.CanGoForward);

        // Tab2 is unaffected
        Assert.Equal(@"C:\X", tab2.CurrentPath);
        Assert.False(tab2.CanGoForward);
    }

    [Fact]
    public void CreateTab_AtSpecificPath_NavigatesToThatPath()
    {
        var tab = _sut.CreateTab(@"C:\Users\Downloads");

        Assert.Equal(@"C:\Users\Downloads", tab.CurrentPath);
        Assert.Equal("Downloads", tab.Title);
    }
}
