using NexusExplorer.App.Helpers;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Tests;

public class SortAndGroupCombinedTests
{
    private static FileSystemItem MakeFile(string name, long size, DateTime? modified = null) => new()
    {
        Name = name,
        Path = $"/test/{name}",
        Type = FileSystemItemType.File,
        Size = size,
        LastModified = modified ?? DateTime.Now
    };

    private static FileSystemItem MakeDir(string name) => new()
    {
        Name = name,
        Path = $"/test/{name}",
        Type = FileSystemItemType.Directory,
        LastModified = DateTime.Now
    };

    [Fact]
    public void SortByName_GroupBySize_ItemsSortedWithinGroups()
    {
        // Simulate: items are pre-sorted by name (as SortItems would do)
        var items = new List<FileSystemItem>
        {
            MakeFile("alpha.txt", 200),     // Small
            MakeFile("beta.txt", 100),      // Small
            MakeFile("gamma.mp4", 5L * 1024 * 1024), // Medium
            MakeFile("delta.mp4", 3L * 1024 * 1024), // Medium
        };

        // Sort by name first (ascending)
        items = items.OrderBy(x => x.Name).ToList();

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.Size).ToList();

        // Within Small group: alpha comes before beta (sorted by name)
        var smallIdx = result.FindIndex(i => i.IsGroupHeader && i.GroupName == "Small");
        Assert.Equal("alpha.txt", result[smallIdx + 1].Name);
        Assert.Equal("beta.txt", result[smallIdx + 2].Name);

        // Within Medium group: delta comes before gamma (sorted by name)
        var medIdx = result.FindIndex(i => i.IsGroupHeader && i.GroupName == "Medium");
        Assert.Equal("delta.mp4", result[medIdx + 1].Name);
        Assert.Equal("gamma.mp4", result[medIdx + 2].Name);
    }

    [Fact]
    public void SortByModified_GroupBySize_ItemsSortedByDateWithinGroups()
    {
        var older = new DateTime(2026, 1, 1);
        var newer = new DateTime(2026, 8, 15);

        var items = new List<FileSystemItem>
        {
            MakeFile("old.txt", 200, older),   // Small
            MakeFile("new.txt", 100, newer),   // Small
        };

        // Sort by modified date ascending
        items = items.OrderBy(x => x.LastModified).ToList();

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.Size).ToList();

        var smallIdx = result.FindIndex(i => i.IsGroupHeader && i.GroupName == "Small");
        Assert.Equal("old.txt", result[smallIdx + 1].Name);   // older first
        Assert.Equal("new.txt", result[smallIdx + 2].Name);   // newer second
    }

    [Fact]
    public void NoGroup_ReturnsListWithoutHeaders()
    {
        var items = new List<FileSystemItem>
        {
            MakeFile("file1.txt", 100),
            MakeFile("file2.txt", 200),
            MakeDir("folder1"),
        };

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.None).ToList();

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, i => i.IsGroupHeader);
    }

    [Fact]
    public void GroupByName_DirectoriesGroupedCorrectly()
    {
        var items = new List<FileSystemItem>
        {
            MakeDir("Archives"),
            MakeDir("Backups"),
            MakeFile("archive.zip", 100),
            MakeFile("backup.dat", 200),
        };

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.Name).ToList();

        var groupHeaders = result.Where(i => i.IsGroupHeader).Select(i => i.GroupName).ToList();
        Assert.Equal(new[] { "A", "B" }, groupHeaders);

        // Both dirs and files starting with A are under A
        var aIdx = result.FindIndex(i => i.IsGroupHeader && i.GroupName == "A");
        var aItems = new List<string>();
        for (int j = aIdx + 1; j < result.Count && !result[j].IsGroupHeader; j++)
            aItems.Add(result[j].Name);

        Assert.Contains("Archives", aItems);
        Assert.Contains("archive.zip", aItems);
    }
}
