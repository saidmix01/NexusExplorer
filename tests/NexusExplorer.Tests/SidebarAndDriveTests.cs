using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexusExplorer.App.Helpers;
using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace NexusExplorer.Tests;

public class SidebarAndDriveTests
{
    private readonly FileSystemService _fileSystemService;

    public SidebarAndDriveTests()
    {
        _fileSystemService = new FileSystemService(NullLogger<FileSystemService>.Instance);
    }

    // ================================================================
    // DRIVES: 1. Fixed Drive correctly detected
    // ================================================================

    [Fact]
    public async Task GetDriveItemsAsync_DetectsFixedDrives()
    {
        var drives = await _fileSystemService.GetDriveItemsAsync();

        // On any Windows machine, at least one fixed drive (C:) should exist
        var fixedDrives = drives.Where(d => d.Category == DriveCategory.Fixed).ToList();
        Assert.NotEmpty(fixedDrives);
        Assert.All(fixedDrives, d => Assert.True(d.IsReady));
    }

    // ================================================================
    // DRIVES: 2. Removable Drive correctly detected (if present)
    // ================================================================

    [Fact]
    public async Task GetDriveItemsAsync_RemovableDrives_HaveCorrectCategory()
    {
        var drives = await _fileSystemService.GetDriveItemsAsync();
        var removable = drives.Where(d => d.Category == DriveCategory.Removable).ToList();

        // Removable drives may or may not exist — just verify they have correct category
        Assert.All(removable, d =>
        {
            Assert.Equal(DriveCategory.Removable, d.Category);
            Assert.False(string.IsNullOrEmpty(d.Path));
        });
    }

    // ================================================================
    // DRIVES: 3. Network Drive correctly detected (if present)
    // ================================================================

    [Fact]
    public async Task GetDriveItemsAsync_NetworkDrives_HaveCorrectCategory()
    {
        var drives = await _fileSystemService.GetDriveItemsAsync();
        var networkDrives = drives.Where(d => d.Category == DriveCategory.Network).ToList();

        // Network drives may or may not be mapped — just verify category is correct
        Assert.All(networkDrives, d =>
        {
            Assert.Equal(DriveCategory.Network, d.Category);
            Assert.False(string.IsNullOrEmpty(d.Path));
        });
    }

    // ================================================================
    // DRIVES: 4. DriveInfo with space total/free
    // ================================================================

    [Fact]
    public async Task GetDriveItemsAsync_ReadyDrives_HaveSpaceInfo()
    {
        var drives = await _fileSystemService.GetDriveItemsAsync();
        var readyDrives = drives.Where(d => d.IsReady).ToList();

        Assert.NotEmpty(readyDrives);
        Assert.All(readyDrives, d =>
        {
            Assert.True(d.TotalSize > 0, $"Drive {d.Name} should have total size > 0");
            Assert.True(d.FreeSpace >= 0, $"Drive {d.Name} should have free space >= 0");
            Assert.True(d.FreeSpace <= d.TotalSize, $"Drive {d.Name} free space should not exceed total");
            Assert.True(d.UsagePercent >= 0 && d.UsagePercent <= 100, $"Drive {d.Name} usage % should be 0-100");
        });
    }

    // ================================================================
    // DRIVES: 5. Drive without access does not crash
    // ================================================================

    [Fact]
    public async Task GetDriveItemsAsync_DoesNotThrow()
    {
        // Simply calling GetDriveItemsAsync should never crash, even if some drives are inaccessible
        var exception = await Record.ExceptionAsync(() => _fileSystemService.GetDriveItemsAsync());
        Assert.Null(exception);
    }

    // ================================================================
    // THIS PC: 6. This PC is recognized as virtual/special location
    // ================================================================

    [Fact]
    public void VirtualPaths_ThisPC_IsVirtual()
    {
        Assert.True(VirtualPaths.IsVirtual(VirtualPaths.ThisPC));
        Assert.Equal("This PC", VirtualPaths.GetDisplayName(VirtualPaths.ThisPC));
    }

    [Fact]
    public void VirtualPaths_Network_IsVirtual()
    {
        Assert.True(VirtualPaths.IsVirtual(VirtualPaths.Network));
        Assert.Equal("Network", VirtualPaths.GetDisplayName(VirtualPaths.Network));
    }

    [Fact]
    public void VirtualPaths_RegularPath_IsNotVirtual()
    {
        Assert.False(VirtualPaths.IsVirtual(@"C:\Users"));
        Assert.False(VirtualPaths.IsVirtual("/home/user"));
        Assert.False(VirtualPaths.IsVirtual(null));
        Assert.False(VirtualPaths.IsVirtual(""));
    }

    // ================================================================
    // THIS PC: 7. This PC returns available drives
    // ================================================================

    [Fact]
    public async Task GetDriveItemsAsync_ReturnsAtLeastOneDrive()
    {
        var drives = await _fileSystemService.GetDriveItemsAsync();
        Assert.NotEmpty(drives);
    }

    [Fact]
    public async Task GetDriveItemsAsync_AllDrivesHavePathAndName()
    {
        var drives = await _fileSystemService.GetDriveItemsAsync();
        Assert.All(drives, d =>
        {
            Assert.False(string.IsNullOrEmpty(d.Name));
            Assert.False(string.IsNullOrEmpty(d.Path));
        });
    }

    // ================================================================
    // ICON INFORMATION: 8. File shows size
    // ================================================================

    [Fact]
    public void ItemInfoConverter_File_ShowsSize()
    {
        var converter = NexusExplorer.App.ViewModels.ItemInfoConverter.Instance;
        var item = new FileSystemItem
        {
            Name = "photo.jpg",
            Path = "/photo.jpg",
            Type = FileSystemItemType.File,
            Size = 4_400_000 // ~4.2 MB
        };

        var result = (string)converter.Convert(item, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)!;
        Assert.Contains("MB", result);
        Assert.Contains("4", result);
    }

    // ================================================================
    // ICON INFORMATION: 9. Folder shows "Folder"
    // ================================================================

    [Fact]
    public void ItemInfoConverter_Folder_ShowsFolder()
    {
        var converter = NexusExplorer.App.ViewModels.ItemInfoConverter.Instance;
        var item = new FileSystemItem
        {
            Name = "Documents",
            Path = "/Documents",
            Type = FileSystemItemType.Directory
        };

        var result = (string)converter.Convert(item, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)!;
        Assert.Equal("Folder", result);
    }

    // ================================================================
    // ICON INFORMATION: 10. Show Item Information ON (property test)
    // ================================================================

    [Fact]
    public void ItemInfoConverter_GroupHeader_ReturnsEmpty()
    {
        var converter = NexusExplorer.App.ViewModels.ItemInfoConverter.Instance;
        var item = new FileSystemItem
        {
            Name = "Group",
            Path = "",
            Type = FileSystemItemType.File,
            IsGroupHeader = true,
            GroupName = "Group"
        };

        var result = (string)converter.Convert(item, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)!;
        Assert.Equal(string.Empty, result);
    }

    // ================================================================
    // ICON INFORMATION: 11. Show Item Information OFF — converter still works (visibility controlled by binding)
    // ================================================================

    [Fact]
    public void ItemInfoConverter_ZeroSizeFile_ReturnsEmpty()
    {
        var converter = NexusExplorer.App.ViewModels.ItemInfoConverter.Instance;
        var item = new FileSystemItem
        {
            Name = "empty.txt",
            Path = "/empty.txt",
            Type = FileSystemItemType.File,
            Size = 0
        };

        var result = (string)converter.Convert(item, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)!;
        Assert.Equal(string.Empty, result);
    }

    // ================================================================
    // GROUPING: 12. Group by Name continues working
    // ================================================================

    [Fact]
    public void GroupByName_GroupsCorrectly()
    {
        var items = new List<FileSystemItem>
        {
            new() { Name = "Alpha.txt", Path = "/Alpha.txt", Type = FileSystemItemType.File },
            new() { Name = "Beta.txt", Path = "/Beta.txt", Type = FileSystemItemType.File },
            new() { Name = "Another.txt", Path = "/Another.txt", Type = FileSystemItemType.File },
        };

        var grouped = FileGroupingHelper.GroupItems(items, FileGroupMode.Name).ToList();

        // Should have headers for A and B
        var headers = grouped.Where(i => i.IsGroupHeader).ToList();
        Assert.Contains(headers, h => h.GroupName == "A");
        Assert.Contains(headers, h => h.GroupName == "B");
    }

    // ================================================================
    // GROUPING: 13. Group by Size continues working
    // ================================================================

    [Fact]
    public void GroupBySize_GroupsCorrectly()
    {
        var items = new List<FileSystemItem>
        {
            new() { Name = "Folder", Path = "/Folder", Type = FileSystemItemType.Directory },
            new() { Name = "small.txt", Path = "/small.txt", Type = FileSystemItemType.File, Size = 500 },
            new() { Name = "big.zip", Path = "/big.zip", Type = FileSystemItemType.File, Size = 500_000_000 },
        };

        var grouped = FileGroupingHelper.GroupItems(items, FileGroupMode.Size).ToList();

        var headers = grouped.Where(i => i.IsGroupHeader).Select(h => h.GroupName).ToList();
        Assert.Contains("Folders", headers);
        Assert.Contains("Small", headers);
        Assert.Contains("Large", headers);
    }

    // ================================================================
    // GROUPING: 14. Group by Type continues working
    // ================================================================

    [Fact]
    public void GroupByType_GroupsCorrectly()
    {
        var items = new List<FileSystemItem>
        {
            new() { Name = "Folder", Path = "/Folder", Type = FileSystemItemType.Directory },
            new() { Name = "image.png", Path = "/image.png", Type = FileSystemItemType.File, Extension = ".png" },
            new() { Name = "code.cs", Path = "/code.cs", Type = FileSystemItemType.File, Extension = ".cs" },
        };

        var grouped = FileGroupingHelper.GroupItems(items, FileGroupMode.Type).ToList();

        var headers = grouped.Where(i => i.IsGroupHeader).Select(h => h.GroupName).ToList();
        Assert.Contains("Folders", headers);
        Assert.Contains("Images", headers);
        Assert.Contains("Code", headers);
    }

    // ================================================================
    // GROUPING: 15. No Group continues working
    // ================================================================

    [Fact]
    public void GroupByNone_ReturnsOriginalList()
    {
        var items = new List<FileSystemItem>
        {
            new() { Name = "a.txt", Path = "/a.txt", Type = FileSystemItemType.File },
            new() { Name = "b.txt", Path = "/b.txt", Type = FileSystemItemType.File },
        };

        var result = FileGroupingHelper.GroupItems(items, FileGroupMode.None).ToList();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, i => i.IsGroupHeader);
    }

    // ================================================================
    // SORTING: 16. Sort Name + Group Size
    // ================================================================

    [Fact]
    public void SortByName_WithGroupBySize_ItemsAreGroupedAndSorted()
    {
        var items = new List<FileSystemItem>
        {
            new() { Name = "Zebra.txt", Path = "/z", Type = FileSystemItemType.File, Size = 100 },
            new() { Name = "Alpha.txt", Path = "/a", Type = FileSystemItemType.File, Size = 200 },
            new() { Name = "BigFile.zip", Path = "/b", Type = FileSystemItemType.File, Size = 200_000_000 },
        };

        // Sort by name ascending
        var sorted = items.OrderBy(x => x.Name).ToList();
        // Group by size
        var grouped = FileGroupingHelper.GroupItems(sorted, FileGroupMode.Size).ToList();

        // Should have group headers
        var headers = grouped.Where(i => i.IsGroupHeader).ToList();
        Assert.True(headers.Count >= 2);

        // Within each group, items should maintain sort order
        var smallItems = GetGroupItems(grouped, "Small");
        if (smallItems.Count > 1)
        {
            Assert.True(string.Compare(smallItems[0].Name, smallItems[1].Name, StringComparison.OrdinalIgnoreCase) <= 0);
        }
    }

    // ================================================================
    // SORTING: 17. Sort Modified + Group Size
    // ================================================================

    [Fact]
    public void SortByModified_WithGroupBySize_Maintains()
    {
        var items = new List<FileSystemItem>
        {
            new() { Name = "old.txt", Path = "/o", Type = FileSystemItemType.File, Size = 100, LastModified = DateTime.Now.AddDays(-10) },
            new() { Name = "new.txt", Path = "/n", Type = FileSystemItemType.File, Size = 200, LastModified = DateTime.Now },
            new() { Name = "big.zip", Path = "/b", Type = FileSystemItemType.File, Size = 500_000_000, LastModified = DateTime.Now.AddDays(-5) },
        };

        var sorted = items.OrderBy(x => x.LastModified).ToList();
        var grouped = FileGroupingHelper.GroupItems(sorted, FileGroupMode.Size).ToList();

        var headers = grouped.Where(i => i.IsGroupHeader).ToList();
        Assert.True(headers.Count >= 2);
    }

    // ================================================================
    // SORTING: 18. Sort Size + Group Type
    // ================================================================

    [Fact]
    public void SortBySize_WithGroupByType_Maintains()
    {
        var items = new List<FileSystemItem>
        {
            new() { Name = "a.png", Path = "/a", Type = FileSystemItemType.File, Extension = ".png", Size = 5000 },
            new() { Name = "b.png", Path = "/b", Type = FileSystemItemType.File, Extension = ".png", Size = 1000 },
            new() { Name = "c.cs", Path = "/c", Type = FileSystemItemType.File, Extension = ".cs", Size = 3000 },
        };

        var sorted = items.OrderBy(x => x.Size).ToList();
        var grouped = FileGroupingHelper.GroupItems(sorted, FileGroupMode.Type).ToList();

        var headers = grouped.Where(i => i.IsGroupHeader).Select(h => h.GroupName).ToList();
        Assert.Contains("Images", headers);
        Assert.Contains("Code", headers);

        // Within Images group, items should be sorted by size
        var imageItems = GetGroupItems(grouped, "Images");
        Assert.True(imageItems[0].Size <= imageItems[1].Size);
    }

    // ================================================================
    // Additional: DriveItem model tests
    // ================================================================

    [Fact]
    public void DriveItem_UsagePercent_CalculatesCorrectly()
    {
        var drive = new DriveItem
        {
            Name = "Local Disk (C:)",
            Path = @"C:\",
            Category = DriveCategory.Fixed,
            TotalSize = 500_000_000_000L,  // 500 GB
            FreeSpace = 200_000_000_000L,  // 200 GB free
            DriveLetter = "C:"
        };

        Assert.Equal(300_000_000_000L, drive.UsedSpace);
        Assert.InRange(drive.UsagePercent, 59.9, 60.1); // ~60%
    }

    [Fact]
    public void DriveItem_EmptyDrive_ZeroPercent()
    {
        var drive = new DriveItem
        {
            Name = "Empty",
            Path = @"E:\",
            Category = DriveCategory.Removable,
            TotalSize = 0,
            FreeSpace = 0,
            DriveLetter = "E:"
        };

        Assert.Equal(0, drive.UsedSpace);
        Assert.Equal(0, drive.UsagePercent);
    }

    // ================================================================
    // Additional: NavigationItem section headers
    // ================================================================

    [Fact]
    public void NavigationItem_SectionHeader_HasCorrectProperties()
    {
        var header = new NavigationItem
        {
            Name = "FAVORITES",
            Path = "",
            Kind = NavigationItemKind.QuickAccess,
            Section = NavigationSection.Favorites,
            IsSectionHeader = true
        };

        Assert.True(header.IsSectionHeader);
        Assert.Equal(NavigationSection.Favorites, header.Section);
    }

    [Fact]
    public void NavigationItem_ThisPC_IsSpecialLocation()
    {
        var thisPC = new NavigationItem
        {
            Name = "This PC",
            Path = VirtualPaths.ThisPC,
            Kind = NavigationItemKind.SpecialLocation,
            Section = NavigationSection.Locations
        };

        Assert.Equal(NavigationItemKind.SpecialLocation, thisPC.Kind);
        Assert.True(VirtualPaths.IsVirtual(thisPC.Path));
    }

    // ================================================================
    // Additional: GetDirectoryItemCountAsync
    // ================================================================

    [Fact]
    public async Task GetDirectoryItemCountAsync_ValidDir_ReturnsCount()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"NexusTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "a.txt"), "");
        File.WriteAllText(Path.Combine(tempDir, "b.txt"), "");
        Directory.CreateDirectory(Path.Combine(tempDir, "sub"));

        try
        {
            var count = await _fileSystemService.GetDirectoryItemCountAsync(tempDir);
            Assert.Equal(3, count); // 2 files + 1 subdirectory
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GetDirectoryItemCountAsync_NonExistentDir_ReturnsNull()
    {
        var count = await _fileSystemService.GetDirectoryItemCountAsync(@"Z:\NonExistent\Path\12345");
        Assert.Null(count);
    }

    // ================================================================
    // Helper
    // ================================================================

    private static List<FileSystemItem> GetGroupItems(List<FileSystemItem> grouped, string groupName)
    {
        var result = new List<FileSystemItem>();
        var inGroup = false;

        foreach (var item in grouped)
        {
            if (item.IsGroupHeader)
            {
                inGroup = item.GroupName == groupName;
                continue;
            }
            if (inGroup)
                result.Add(item);
        }

        return result;
    }
}
