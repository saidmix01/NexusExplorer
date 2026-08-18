using NexusExplorer.App.Helpers;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Tests;

public class GroupByNameTests
{
    private static FileSystemItem MakeFile(string name) => new()
    {
        Name = name,
        Path = $"/test/{name}",
        Type = FileSystemItemType.File,
        Size = 100
    };

    private static FileSystemItem MakeDir(string name) => new()
    {
        Name = name,
        Path = $"/test/{name}",
        Type = FileSystemItemType.Directory
    };

    [Fact]
    public void GetNameGroup_FileStartingWithA_ReturnsA()
    {
        var item = MakeFile("archivo.txt");
        Assert.Equal("A", FileGroupingHelper.GetNameGroup(item));
    }

    [Fact]
    public void GetNameGroup_FileStartingWithUpperB_ReturnsB()
    {
        var item = MakeFile("ArchivoB.txt");
        Assert.Equal("A", FileGroupingHelper.GetNameGroup(item));
    }

    [Fact]
    public void GetNameGroup_FileStartingWithC_ReturnsC()
    {
        var item = MakeFile("config.json");
        Assert.Equal("C", FileGroupingHelper.GetNameGroup(item));
    }

    [Fact]
    public void GetNameGroup_FileStartingWithNumber_ReturnsHash()
    {
        var item = MakeFile("123.txt");
        Assert.Equal("#", FileGroupingHelper.GetNameGroup(item));
    }

    [Fact]
    public void GetNameGroup_FileStartingWithUnderscore_ReturnsHash()
    {
        var item = MakeFile("_config.json");
        Assert.Equal("#", FileGroupingHelper.GetNameGroup(item));
    }

    [Fact]
    public void GetNameGroup_IsCaseInsensitive()
    {
        var lower = MakeFile("archivo.txt");
        var upper = MakeFile("Archivo.txt");

        Assert.Equal("A", FileGroupingHelper.GetNameGroup(lower));
        Assert.Equal("A", FileGroupingHelper.GetNameGroup(upper));
    }

    [Fact]
    public void GetNameGroup_DirectoryIsGroupedByFirstLetter()
    {
        var dir = MakeDir("Documents");
        Assert.Equal("D", FileGroupingHelper.GetNameGroup(dir));
    }

    [Fact]
    public void GroupItems_ByName_ProducesAlphabeticalGroups()
    {
        var items = new List<FileSystemItem>
        {
            MakeFile("archivo.txt"),
            MakeFile("app.exe"),
            MakeFile("backup.zip"),
            MakeFile("config.json"),
            MakeFile("123.txt"),
            MakeFile("_data.csv")
        };

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.Name).ToList();

        // Expected groups in order: #, A, B, C
        var groupHeaders = result.Where(i => i.IsGroupHeader).Select(i => i.GroupName).ToList();
        Assert.Equal(new[] { "#", "A", "B", "C" }, groupHeaders);
    }

    [Fact]
    public void GroupItems_ByName_ItemsUnderCorrectGroups()
    {
        var items = new List<FileSystemItem>
        {
            MakeFile("archivo.txt"),
            MakeFile("backup.zip"),
            MakeFile("123.txt"),
        };

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.Name).ToList();

        // Find items after each group header
        var hashIdx = result.FindIndex(i => i.IsGroupHeader && i.GroupName == "#");
        var aIdx = result.FindIndex(i => i.IsGroupHeader && i.GroupName == "A");
        var bIdx = result.FindIndex(i => i.IsGroupHeader && i.GroupName == "B");

        Assert.Equal("123.txt", result[hashIdx + 1].Name);
        Assert.Equal("archivo.txt", result[aIdx + 1].Name);
        Assert.Equal("backup.zip", result[bIdx + 1].Name);
    }

    [Fact]
    public void GroupItems_NoGroup_ReturnsItemsWithoutHeaders()
    {
        var items = new List<FileSystemItem>
        {
            MakeFile("archivo.txt"),
            MakeFile("backup.zip"),
        };

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.None).ToList();

        Assert.DoesNotContain(result, i => i.IsGroupHeader);
        Assert.Equal(2, result.Count);
    }
}
