using NexusExplorer.Core.Services;

namespace NexusExplorer.Tests;

public class NavigationServiceTests
{
    private readonly NavigationService _sut = new();

    [Fact]
    public void NavigateTo_SetsCurrentPath()
    {
        _sut.NavigateTo(@"C:\Users");

        Assert.Equal(@"C:\Users", _sut.CurrentPath);
    }

    [Fact]
    public void NavigateTo_FiresNavigatedEvent()
    {
        string? navigatedPath = null;
        _sut.Navigated += (_, path) => navigatedPath = path;

        _sut.NavigateTo(@"C:\Users");

        Assert.Equal(@"C:\Users", navigatedPath);
    }

    [Fact]
    public void NavigateTo_SamePath_DoesNotFireEvent()
    {
        _sut.NavigateTo(@"C:\Users");
        var eventFired = false;
        _sut.Navigated += (_, _) => eventFired = true;

        _sut.NavigateTo(@"C:\Users");

        Assert.False(eventFired);
    }

    [Fact]
    public void NavigateTo_SamePathDifferentCase_DoesNotFireEvent()
    {
        _sut.NavigateTo(@"C:\Users");
        var eventFired = false;
        _sut.Navigated += (_, _) => eventFired = true;

        _sut.NavigateTo(@"c:\users");

        Assert.False(eventFired);
    }

    [Fact]
    public void GoBack_ReturnsToPreviousPath()
    {
        _sut.NavigateTo(@"C:\Users");
        _sut.NavigateTo(@"C:\Users\Documents");

        _sut.GoBack();

        Assert.Equal(@"C:\Users", _sut.CurrentPath);
    }

    [Fact]
    public void GoBack_WhenNoHistory_DoesNothing()
    {
        _sut.NavigateTo(@"C:\Users");

        _sut.GoBack();

        Assert.Equal(@"C:\Users", _sut.CurrentPath);
    }

    [Fact]
    public void CanGoBack_IsFalse_Initially()
    {
        Assert.False(_sut.CanGoBack);
    }

    [Fact]
    public void CanGoBack_IsTrue_AfterNavigation()
    {
        _sut.NavigateTo(@"C:\Users");
        _sut.NavigateTo(@"C:\Users\Documents");

        Assert.True(_sut.CanGoBack);
    }

    [Fact]
    public void GoForward_ReturnsToNextPath()
    {
        _sut.NavigateTo(@"C:\Users");
        _sut.NavigateTo(@"C:\Users\Documents");
        _sut.GoBack();

        _sut.GoForward();

        Assert.Equal(@"C:\Users\Documents", _sut.CurrentPath);
    }

    [Fact]
    public void GoForward_WhenNoForwardHistory_DoesNothing()
    {
        _sut.NavigateTo(@"C:\Users");

        _sut.GoForward();

        Assert.Equal(@"C:\Users", _sut.CurrentPath);
    }

    [Fact]
    public void CanGoForward_IsFalse_Initially()
    {
        Assert.False(_sut.CanGoForward);
    }

    [Fact]
    public void CanGoForward_IsTrue_AfterGoBack()
    {
        _sut.NavigateTo(@"C:\Users");
        _sut.NavigateTo(@"C:\Users\Documents");
        _sut.GoBack();

        Assert.True(_sut.CanGoForward);
    }

    [Fact]
    public void NavigateTo_NewPath_ClearsForwardHistory()
    {
        _sut.NavigateTo(@"C:\Users");
        _sut.NavigateTo(@"C:\Users\Documents");
        _sut.GoBack();

        _sut.NavigateTo(@"C:\Windows");

        Assert.False(_sut.CanGoForward);
    }

    [Fact]
    public void GoUp_NavigatesToParentDirectory()
    {
        _sut.NavigateTo(@"C:\Users\Documents");

        _sut.GoUp();

        Assert.Equal(@"C:\Users", _sut.CurrentPath);
    }

    [Fact]
    public void GoUp_AtRoot_DoesNotCrash()
    {
        _sut.NavigateTo(@"C:\");

        _sut.GoUp();

        Assert.NotNull(_sut.CurrentPath);
    }

    [Fact]
    public void CanGoUp_IsFalse_WhenNoPath()
    {
        Assert.False(_sut.CanGoUp);
    }

    [Fact]
    public void CanGoUp_IsTrue_WhenPathHasParent()
    {
        _sut.NavigateTo(@"C:\Users\Documents");

        Assert.True(_sut.CanGoUp);
    }

    [Fact]
    public void Refresh_FiresNavigatedEvent_WithCurrentPath()
    {
        _sut.NavigateTo(@"C:\Users");
        string? navigatedPath = null;
        _sut.Navigated += (_, path) => navigatedPath = path;

        _sut.Refresh();

        Assert.Equal(@"C:\Users", navigatedPath);
    }

    [Fact]
    public void Refresh_DoesNotChangeHistory()
    {
        _sut.NavigateTo(@"C:\Users");
        _sut.NavigateTo(@"C:\Users\Documents");

        _sut.Refresh();

        Assert.True(_sut.CanGoBack);
        Assert.Equal(@"C:\Users\Documents", _sut.CurrentPath);
    }

    [Fact]
    public void MultipleBackForward_WorksCorrectly()
    {
        _sut.NavigateTo(@"C:\A");
        _sut.NavigateTo(@"C:\B");
        _sut.NavigateTo(@"C:\C");

        _sut.GoBack();
        Assert.Equal(@"C:\B", _sut.CurrentPath);

        _sut.GoBack();
        Assert.Equal(@"C:\A", _sut.CurrentPath);

        _sut.GoForward();
        Assert.Equal(@"C:\B", _sut.CurrentPath);

        _sut.GoForward();
        Assert.Equal(@"C:\C", _sut.CurrentPath);
    }

    [Fact]
    public void GoBack_ThenNewNavigation_ClearsForwardHistory()
    {
        _sut.NavigateTo(@"C:\A");
        _sut.NavigateTo(@"C:\B");
        _sut.NavigateTo(@"C:\C");

        _sut.GoBack();
        _sut.NavigateTo(@"C:\D");

        Assert.False(_sut.CanGoForward);
        Assert.Equal(@"C:\D", _sut.CurrentPath);

        _sut.GoBack();
        Assert.Equal(@"C:\B", _sut.CurrentPath);
    }
}
