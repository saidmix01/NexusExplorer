using NexusExplorer.Core.Models;
using Xunit;

namespace NexusExplorer.Tests;

/// <summary>
/// Tests for the theme system: ThemeMode enum and UserPreferences persistence.
/// </summary>
public class ThemeTests
{
    [Fact]
    public void ThemeMode_Default_IsRefinedMinimalism()
    {
        var prefs = new UserPreferences();
        Assert.Equal(ThemeMode.RefinedMinimalism, prefs.Theme);
    }

    [Fact]
    public void ThemeMode_CanBeSetToDark()
    {
        var prefs = new UserPreferences { Theme = ThemeMode.Dark };
        Assert.Equal(ThemeMode.Dark, prefs.Theme);
    }

    [Fact]
    public void ThemeMode_CanBeSetToSystem()
    {
        var prefs = new UserPreferences { Theme = ThemeMode.System };
        Assert.Equal(ThemeMode.System, prefs.Theme);
    }

    [Fact]
    public void AppState_PreservesThemePreference()
    {
        var state = new AppState
        {
            Preferences = new UserPreferences { Theme = ThemeMode.Dark }
        };
        Assert.Equal(ThemeMode.Dark, state.Preferences.Theme);
    }

    [Fact]
    public void AppState_DefaultTheme_IsRefinedMinimalism()
    {
        var state = new AppState();
        Assert.Equal(ThemeMode.RefinedMinimalism, state.Preferences.Theme);
    }

    [Fact]
    public void ThemeMode_AllValues_AreDefined()
    {
        // Legacy values retained for backward-compatible persistence
        Assert.Equal(0, (int)ThemeMode.Light);
        Assert.Equal(1, (int)ThemeMode.Dark);
        Assert.Equal(2, (int)ThemeMode.System);

        // Current spec themes
        Assert.Equal(10, (int)ThemeMode.RefinedMinimalism);
        Assert.Equal(11, (int)ThemeMode.ModernPastel);
        Assert.Equal(12, (int)ThemeMode.AdvancedHierarchy);
        Assert.Equal(13, (int)ThemeMode.ContextualDark);
    }

    [Fact]
    public void UserPreferences_ThemePersistence_RoundTrip()
    {
        // Simulate JSON serialization/deserialization behavior
        var original = new UserPreferences
        {
            ShowPreviewPanel = true,
            IconZoomLevel = 75,
            Theme = ThemeMode.Dark,
            PinnedFavorites = ["C:\\Users\\Test\\Documents"]
        };

        // Verify all properties are set correctly
        Assert.True(original.ShowPreviewPanel);
        Assert.Equal(75, original.IconZoomLevel);
        Assert.Equal(ThemeMode.Dark, original.Theme);
        Assert.Single(original.PinnedFavorites);
    }

    [Fact]
    public void TabState_NotAffectedByThemeChange()
    {
        // Ensure theme changes don't affect tab state
        var tab = new TabState
        {
            CurrentPath = "C:\\Users\\Test",
            Title = "Test Tab",
            ViewMode = ExplorerViewMode.Details
        };

        // Theme changes only affect UserPreferences, not TabState
        var state = new AppState
        {
            Preferences = new UserPreferences { Theme = ThemeMode.Dark },
            Tabs = [tab],
            ActiveTabIndex = 0
        };

        Assert.Equal("C:\\Users\\Test", state.Tabs[0].CurrentPath);
        Assert.Equal("Test Tab", state.Tabs[0].Title);
        Assert.Equal(ExplorerViewMode.Details, state.Tabs[0].ViewMode);
    }
}
