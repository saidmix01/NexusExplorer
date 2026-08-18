using NexusExplorer.App.Helpers;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Tests;

public class GroupBySizeTests
{
    private static FileSystemItem MakeFile(string name, long size) => new()
    {
        Name = name,
        Path = $"/test/{name}",
        Type = FileSystemItemType.File,
        Size = size
    };

    private static FileSystemItem MakeDir(string name) => new()
    {
        Name = name,
        Path = $"/test/{name}",
        Type = FileSystemItemType.Directory
    };

    [Fact]
    public void GetSizeGroup_ZeroBytes_ReturnsEmpty()
    {
        var item = MakeFile("empty.txt", 0);
        Assert.Equal("Empty", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_1KB_ReturnsSmall()
    {
        var item = MakeFile("small.txt", 1024); // 1 KB
        Assert.Equal("Small", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_5MB_ReturnsMedium()
    {
        var item = MakeFile("medium.mp4", 5L * 1024 * 1024); // 5 MB
        Assert.Equal("Medium", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_500MB_ReturnsLarge()
    {
        var item = MakeFile("large.zip", 500L * 1024 * 1024); // 500 MB
        Assert.Equal("Large", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_2GB_ReturnsHuge()
    {
        var item = MakeFile("huge.iso", 2L * 1024 * 1024 * 1024); // 2 GB
        Assert.Equal("Huge", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_Directory_ReturnsFolders()
    {
        var item = MakeDir("Documents");
        Assert.Equal("Folders", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_ExactlyOneMB_ReturnsSmall()
    {
        // 1 MB boundary: exactly 1 MB should be "Small" (1 B - 1 MB inclusive)
        var item = MakeFile("boundary.bin", 1L * 1024 * 1024);
        Assert.Equal("Small", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_JustOverOneMB_ReturnsMedium()
    {
        var item = MakeFile("over1mb.bin", 1L * 1024 * 1024 + 1);
        Assert.Equal("Medium", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_Exactly100MB_ReturnsMedium()
    {
        var item = MakeFile("boundary100.bin", 100L * 1024 * 1024);
        Assert.Equal("Medium", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_JustOver100MB_ReturnsLarge()
    {
        var item = MakeFile("over100mb.bin", 100L * 1024 * 1024 + 1);
        Assert.Equal("Large", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_Exactly1GB_ReturnsLarge()
    {
        var item = MakeFile("boundary1gb.bin", 1L * 1024 * 1024 * 1024);
        Assert.Equal("Large", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GetSizeGroup_JustOver1GB_ReturnsHuge()
    {
        var item = MakeFile("over1gb.bin", 1L * 1024 * 1024 * 1024 + 1);
        Assert.Equal("Huge", FileGroupingHelper.GetSizeGroup(item));
    }

    [Fact]
    public void GroupItems_BySize_FoldersGroupComesFirst()
    {
        var items = new List<FileSystemItem>
        {
            MakeDir("Documents"),
            MakeDir("Pictures"),
            MakeFile("empty.txt", 0),
            MakeFile("config.json", 512),
            MakeFile("video.mp4", 50L * 1024 * 1024),
            MakeFile("backup.zip", 500L * 1024 * 1024),
            MakeFile("disk.iso", 2L * 1024 * 1024 * 1024),
        };

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.Size).ToList();
        var groupHeaders = result.Where(i => i.IsGroupHeader).Select(i => i.GroupName).ToList();

        // Folders should come first
        Assert.Equal("Folders", groupHeaders[0]);
        
        // All expected groups present in correct order
        Assert.Equal(new[] { "Folders", "Empty", "Small", "Medium", "Large", "Huge" }, groupHeaders);
    }

    [Fact]
    public void GroupItems_BySize_SortWithinGroupsPreserved()
    {
        // Items pre-sorted by name (simulating Sort by Name applied before grouping)
        var items = new List<FileSystemItem>
        {
            MakeFile("alpha.txt", 100),      // Small
            MakeFile("beta.txt", 200),       // Small
            MakeFile("gamma.txt", 50L * 1024 * 1024),  // Medium
        };

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.Size).ToList();

        // Within the Small group, order should be preserved: alpha, beta
        var smallIdx = result.FindIndex(i => i.IsGroupHeader && i.GroupName == "Small");
        Assert.Equal("alpha.txt", result[smallIdx + 1].Name);
        Assert.Equal("beta.txt", result[smallIdx + 2].Name);
    }

    [Fact]
    public void GroupItems_BySize_OnlyPresentGroupsAppear()
    {
        var items = new List<FileSystemItem>
        {
            MakeFile("small.txt", 100),
            MakeFile("medium.bin", 5L * 1024 * 1024),
        };

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.Size).ToList();
        var groupHeaders = result.Where(i => i.IsGroupHeader).Select(i => i.GroupName).ToList();

        // Only Small and Medium should appear (no Folders, Empty, Large, Huge)
        Assert.Equal(new[] { "Small", "Medium" }, groupHeaders);
    }
}
