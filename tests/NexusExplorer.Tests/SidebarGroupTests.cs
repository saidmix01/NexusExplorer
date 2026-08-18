using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using NexusExplorer.App.ViewModels;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Tests;

public class SidebarGroupTests
{
    // ================================================================
    // MODEL: expand / collapse
    // ================================================================

    [Fact]
    public void SidebarGroup_IsExpanded_DefaultsToTrue_AndRaisesChange()
    {
        var group = new SidebarGroup { Id = "g1", Name = "Dev" };
        var changed = new List<string>();
        group.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        Assert.True(group.IsExpanded);

        group.IsExpanded = false;
        group.IsExpanded = false; // no change -> no event
        group.IsExpanded = true;

        Assert.Equal(new[] { "IsExpanded", "IsExpanded" }, changed);
    }

    // ================================================================
    // MODEL: rename
    // ================================================================

    [Fact]
    public void SidebarGroup_Rename_UpdatesName_AndRaisesChange()
    {
        var group = new SidebarGroup { Id = "g1", Name = "Dev" };
        var changed = new List<string>();
        group.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        group.Name = "Development";

        Assert.Equal("Development", group.Name);
        Assert.Contains("Name", changed);
    }

    // ================================================================
    // MODEL: items order is preserved
    // ================================================================

    [Fact]
    public void SidebarGroup_Items_PreserveUserOrder()
    {
        var group = new SidebarGroup { Id = "g", Name = "G" };
        group.Items.Add(new NavigationItem { Name = "a", Path = @"C:\a" });
        group.Items.Add(new NavigationItem { Name = "b", Path = @"C:\b" });
        group.Items.Add(new NavigationItem { Name = "c", Path = @"C:\c" });

        Assert.Equal(new[] { "a", "b", "c" }, group.Items.Select(i => i.Name));

        group.Items.Move(0, 2);
        Assert.Equal(new[] { "b", "c", "a" }, group.Items.Select(i => i.Name));
    }

    [Fact]
    public void SidebarGroup_AddRemoveItem_Works()
    {
        var group = new SidebarGroup { Id = "g", Name = "G" };
        var item = new NavigationItem { Name = "Nexus", Path = @"C:\Projects\Nexus", Kind = NavigationItemKind.Custom };

        group.Items.Add(item);
        Assert.Single(group.Items);

        Assert.True(group.Items.Remove(item));
        Assert.Empty(group.Items);
    }

    // ================================================================
    // MODEL: system group ids
    // ================================================================

    [Fact]
    public void SidebarGroupIds_HaveExpectedValues()
    {
        Assert.Equal("favorites", SidebarGroupIds.Favorites);
        Assert.Equal("locations", SidebarGroupIds.Locations);
        Assert.Equal("network", SidebarGroupIds.Network);
    }

    // ================================================================
    // MODEL: NavigationItem availability defaults
    // ================================================================

    [Fact]
    public void NavigationItem_IsAvailable_DefaultsToTrue()
    {
        var item = new NavigationItem { Name = "Home", Path = @"C:\Users" };
        Assert.True(item.IsAvailable);
        Assert.Equal(NavigationItemKind.QuickAccess, item.Kind);
    }

    // ================================================================
    // CONVERTERS
    // ================================================================

    [Fact]
    public void IsExpandedToGlyphConverter_ReturnsCorrectGlyphs()
    {
        var converter = IsExpandedToGlyphConverter.Instance;

        Assert.Equal("\u25BE", (string)converter.Convert(true, typeof(string), null, CultureInfo.InvariantCulture)!);
        Assert.Equal("\u25B8", (string)converter.Convert(false, typeof(string), null, CultureInfo.InvariantCulture)!);
    }

    [Fact]
    public void IsCustomItemConverter_DetectsCustomKind()
    {
        var converter = IsCustomItemConverter.Instance;

        Assert.True((bool)converter.Convert(NavigationItemKind.Custom, typeof(bool), null, CultureInfo.InvariantCulture)!);
        Assert.False((bool)converter.Convert(NavigationItemKind.Favorite, typeof(bool), null, CultureInfo.InvariantCulture)!);
    }

    [Fact]
    public void IsAvailableToOpacityConverter_DimsUnavailableItems()
    {
        var converter = IsAvailableToOpacityConverter.Instance;

        Assert.Equal(0.55, (double)converter.Convert(false, typeof(double), null, CultureInfo.InvariantCulture)!);
        Assert.Equal(1.0, (double)converter.Convert(true, typeof(double), null, CultureInfo.InvariantCulture)!);
    }

    // ================================================================
    // PERSISTENCE: group state round-trips through JSON
    // ================================================================

    [Fact]
    public void SidebarGroupState_RoundTripsThroughJson()
    {
        var state = new AppState
        {
            Window = new WindowBoundsState { X = 0, Y = 0 },
            SidebarGroups =
            [
                new SidebarGroupState { Id = "favorites", Name = "FAVORITES", IsSystem = true, IsExpanded = true },
                new SidebarGroupState
                {
                    Id = "dev",
                    Name = "Development",
                    IsSystem = false,
                    IsExpanded = false,
                    Items = [ new SidebarItemState { Path = @"C:\Projects\Nexus", DisplayName = "Nexus" } ]
                }
            ]
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(state, options);
        var restored = JsonSerializer.Deserialize<AppState>(json, options)!;

        Assert.Equal(2, restored.SidebarGroups.Count);

        var dev = restored.SidebarGroups.Single(g => g.Id == "dev");
        Assert.False(dev.IsExpanded);
        Assert.Equal(@"C:\Projects\Nexus", dev.Items[0].Path);
        Assert.Equal("Nexus", dev.Items[0].DisplayName);
    }
}
