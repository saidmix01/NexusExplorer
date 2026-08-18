using NexusExplorer.App.Helpers;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Tests;

public class HiddenFilesFilterTests
{
    private static FileSystemItem MakeFile(string name, bool isHidden = false) => new()
    {
        Name = name,
        Path = $"/test/{name}",
        Type = FileSystemItemType.File,
        Size = 100,
        IsHidden = isHidden
    };

    private static FileSystemItem MakeDir(string name, bool isHidden = false) => new()
    {
        Name = name,
        Path = $"/test/{name}",
        Type = FileSystemItemType.Directory,
        IsHidden = isHidden
    };

    [Fact]
    public void FilterHidden_NormalFile_AlwaysVisible()
    {
        var items = new[] { MakeFile("readme.txt") };

        var resultHidden = FileGroupingHelper.FilterHidden(items, showHidden: false).ToList();
        var resultShown = FileGroupingHelper.FilterHidden(items, showHidden: true).ToList();

        Assert.Single(resultHidden);
        Assert.Single(resultShown);
    }

    [Fact]
    public void FilterHidden_HiddenFile_HiddenWhenOff()
    {
        var items = new[] { MakeFile("secret.dat", isHidden: true) };

        var result = FileGroupingHelper.FilterHidden(items, showHidden: false).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void FilterHidden_HiddenFile_VisibleWhenOn()
    {
        var items = new[] { MakeFile("secret.dat", isHidden: true) };

        var result = FileGroupingHelper.FilterHidden(items, showHidden: true).ToList();

        Assert.Single(result);
    }

    [Fact]
    public void FilterHidden_HiddenFolder_HiddenWhenOff()
    {
        var items = new[] { MakeDir("$Recycle.Bin", isHidden: true) };

        var result = FileGroupingHelper.FilterHidden(items, showHidden: false).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void FilterHidden_HiddenFolder_VisibleWhenOn()
    {
        var items = new[] { MakeDir("$Recycle.Bin", isHidden: true) };

        var result = FileGroupingHelper.FilterHidden(items, showHidden: true).ToList();

        Assert.Single(result);
    }

    [Fact]
    public void FilterHidden_DotPrefixedFile_NotHiddenByNameAlone()
    {
        // On Windows, a file starting with "." is NOT hidden unless it has the Hidden attribute.
        // This verifies the filter only checks IsHidden, not the name.
        var item = MakeFile(".test.txt", isHidden: false);
        var items = new[] { item };

        var result = FileGroupingHelper.FilterHidden(items, showHidden: false).ToList();

        Assert.Single(result);
        Assert.Equal(".test.txt", result[0].Name);
    }

    [Fact]
    public void FilterHidden_MixedItems_OnlyHiddenFiltered()
    {
        var items = new[]
        {
            MakeFile("visible.txt"),
            MakeFile("hidden.sys", isHidden: true),
            MakeDir("Normal"),
            MakeDir("HiddenDir", isHidden: true),
            MakeFile(".gitignore", isHidden: false), // dot-prefixed but not hidden
        };

        var result = FileGroupingHelper.FilterHidden(items, showHidden: false).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, i => i.Name == "visible.txt");
        Assert.Contains(result, i => i.Name == "Normal");
        Assert.Contains(result, i => i.Name == ".gitignore");
        Assert.DoesNotContain(result, i => i.Name == "hidden.sys");
        Assert.DoesNotContain(result, i => i.Name == "HiddenDir");
    }

    [Fact]
    public void FilterHidden_ShowHiddenTrue_DoesNotFilterAnything()
    {
        var items = new[]
        {
            MakeFile("visible.txt"),
            MakeFile("hidden.sys", isHidden: true),
            MakeDir("Normal"),
            MakeDir("HiddenDir", isHidden: true),
        };

        var result = FileGroupingHelper.FilterHidden(items, showHidden: true).ToList();

        Assert.Equal(4, result.Count);
    }
}
