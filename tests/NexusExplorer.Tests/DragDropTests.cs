using NexusExplorer.Core.Models;
using Xunit;

namespace NexusExplorer.Tests;

public class DragDropTests
{
    // ================================================================
    // 1. Drag single file
    // ================================================================
    [Fact]
    public void Validate_SingleFile_ToValidFolder_IsValid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\Users\Said\Documents\file.txt"],
            @"C:\Users\Said\Pictures",
            isInternal: true,
            sourceDirectory: @"C:\Users\Said\Documents");

        Assert.True(result.IsValid);
        Assert.Equal(DropEffect.Move, result.Effect);
    }

    // ================================================================
    // 2. Drag multiple files
    // ================================================================
    [Fact]
    public void Validate_MultipleFiles_ToValidFolder_IsValid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\a\file1.txt", @"C:\a\file2.txt", @"C:\a\file3.txt"],
            @"C:\b",
            isInternal: true,
            sourceDirectory: @"C:\a");

        Assert.True(result.IsValid);
        Assert.Equal(DropEffect.Move, result.Effect);
    }

    // ================================================================
    // 3. Drag folder
    // ================================================================
    [Fact]
    public void Validate_Folder_ToValidDestination_IsValid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\Users\Said\Documents\MyFolder"],
            @"C:\Users\Said\Pictures",
            isInternal: true,
            sourceDirectory: @"C:\Users\Said\Documents");

        Assert.True(result.IsValid);
    }

    // ================================================================
    // 4. Drag multiple folders
    // ================================================================
    [Fact]
    public void Validate_MultipleFolders_IsValid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\a\folder1", @"C:\a\folder2"],
            @"C:\b",
            isInternal: true,
            sourceDirectory: @"C:\a");

        Assert.True(result.IsValid);
    }

    // ================================================================
    // 5. Drop into folder
    // ================================================================
    [Fact]
    public void ValidateDropOnItem_Folder_IsValid()
    {
        var targetFolder = new FileSystemItem
        {
            Name = "Pictures",
            Path = @"C:\Users\Said\Pictures",
            Type = FileSystemItemType.Directory
        };

        var result = DragDropValidator.ValidateDropOnItem(
            [@"C:\Users\Said\Documents\photo.jpg"],
            targetFolder,
            isInternal: true,
            sourceDirectory: @"C:\Users\Said\Documents");

        Assert.True(result.IsValid);
    }

    // ================================================================
    // 6. Drop into empty area (uses CurrentPath)
    // ================================================================
    [Fact]
    public void Validate_DropToCurrentPath_DifferentFromSource_IsValid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\source\file.txt"],
            @"C:\destination",
            isInternal: true,
            sourceDirectory: @"C:\source");

        Assert.True(result.IsValid);
    }

    // ================================================================
    // 7. Invalid destination (drop on file, not folder)
    // ================================================================
    [Fact]
    public void ValidateDropOnItem_File_IsInvalid()
    {
        var targetFile = new FileSystemItem
        {
            Name = "document.pdf",
            Path = @"C:\Users\Said\document.pdf",
            Type = FileSystemItemType.File
        };

        var result = DragDropValidator.ValidateDropOnItem(
            [@"C:\Users\Said\photo.jpg"],
            targetFile,
            isInternal: true,
            sourceDirectory: @"C:\Users\Said");

        Assert.False(result.IsValid);
        Assert.Contains("file", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // 8. Self-drop (drop file into same directory)
    // ================================================================
    [Fact]
    public void Validate_SelfDrop_SameSourceAndDestination_IsInvalid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\Users\Said\Documents\file.txt"],
            @"C:\Users\Said\Documents",
            isInternal: true,
            sourceDirectory: @"C:\Users\Said\Documents");

        Assert.False(result.IsValid);
        Assert.Contains("already", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // 9. Folder into itself
    // ================================================================
    [Fact]
    public void Validate_FolderIntoItself_IsInvalid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\Users\Said\Documents"],
            @"C:\Users\Said\Documents",
            isInternal: true,
            sourceDirectory: @"C:\Users\Said");

        Assert.False(result.IsValid);
        Assert.Contains("itself", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // 10. Folder into descendant
    // ================================================================
    [Fact]
    public void Validate_FolderIntoDescendant_IsInvalid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\Users\Said\Documents"],
            @"C:\Users\Said\Documents\SubFolder\Deep",
            isInternal: true,
            sourceDirectory: @"C:\Users\Said");

        Assert.False(result.IsValid);
        Assert.Contains("subfolder", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // 11. Internal Nexus drag = Move
    // ================================================================
    [Fact]
    public void Validate_InternalDrag_EffectIsMove()
    {
        var result = DragDropValidator.Validate(
            [@"C:\a\file.txt"],
            @"C:\b",
            isInternal: true,
            sourceDirectory: @"C:\a");

        Assert.True(result.IsValid);
        Assert.Equal(DropEffect.Move, result.Effect);
    }

    // ================================================================
    // 12. External Explorer drag = Copy
    // ================================================================
    [Fact]
    public void Validate_ExternalDrag_EffectIsCopy()
    {
        var result = DragDropValidator.Validate(
            [@"C:\external\file.txt"],
            @"C:\Users\Said\Documents",
            isInternal: false,
            sourceDirectory: null);

        Assert.True(result.IsValid);
        Assert.Equal(DropEffect.Copy, result.Effect);
    }

    // ================================================================
    // 13. Conflict resolution (validated externally by FileOperationService)
    // — here we just verify the drop itself is valid
    // ================================================================
    [Fact]
    public void Validate_FileToFolderThatMayHaveConflict_IsStillValid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\source\photo.jpg"],
            @"C:\destination",
            isInternal: true,
            sourceDirectory: @"C:\source");

        // The validator doesn't check conflicts (that's FileOperationService's job)
        Assert.True(result.IsValid);
    }

    // ================================================================
    // 14. Multiple selection drag
    // ================================================================
    [Fact]
    public void Validate_MultipleSelection_AllValid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\a\file1.txt", @"C:\a\file2.txt", @"C:\a\folder1"],
            @"C:\b",
            isInternal: true,
            sourceDirectory: @"C:\a");

        Assert.True(result.IsValid);
        Assert.Equal(DropEffect.Move, result.Effect);
    }

    // ================================================================
    // 15. Different tab destination (same as folder-to-folder validation)
    // ================================================================
    [Fact]
    public void Validate_CrossTabDrag_IsValid()
    {
        // Tab A current path: C:\Users\Said\Documents
        // Tab B current path: C:\Users\Said\Pictures
        // Dragging from A to B
        var result = DragDropValidator.Validate(
            [@"C:\Users\Said\Documents\file.txt"],
            @"C:\Users\Said\Pictures",
            isInternal: true,
            sourceDirectory: @"C:\Users\Said\Documents");

        Assert.True(result.IsValid);
    }

    // ================================================================
    // 16. Split panel destination (same validation as cross-tab)
    // ================================================================
    [Fact]
    public void Validate_CrossPanelDrag_IsValid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\left\file.txt"],
            @"C:\right",
            isInternal: true,
            sourceDirectory: @"C:\left");

        Assert.True(result.IsValid);
    }

    // ================================================================
    // Additional edge cases
    // ================================================================

    [Fact]
    public void Validate_EmptySourcePaths_IsInvalid()
    {
        var result = DragDropValidator.Validate(
            Array.Empty<string>(),
            @"C:\destination",
            isInternal: true,
            sourceDirectory: @"C:\source");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyDestination_IsInvalid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\a\file.txt"],
            "",
            isInternal: true,
            sourceDirectory: @"C:\a");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_VirtualPathDestination_IsInvalid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\a\file.txt"],
            VirtualPaths.ThisPC,
            isInternal: true,
            sourceDirectory: @"C:\a");

        Assert.False(result.IsValid);
        Assert.Contains("virtual", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsDescendant_ChildPath_ReturnsTrue()
    {
        Assert.True(DragDropValidator.IsDescendant(@"C:\Parent", @"C:\Parent\Child"));
        Assert.True(DragDropValidator.IsDescendant(@"C:\Parent", @"C:\Parent\Child\Deep\Nested"));
    }

    [Fact]
    public void IsDescendant_SiblingPath_ReturnsFalse()
    {
        Assert.False(DragDropValidator.IsDescendant(@"C:\Parent", @"C:\Parent2\Something"));
        Assert.False(DragDropValidator.IsDescendant(@"C:\A", @"C:\B"));
    }

    [Fact]
    public void IsDescendant_SamePath_ReturnsFalse()
    {
        Assert.False(DragDropValidator.IsDescendant(@"C:\Parent", @"C:\Parent"));
    }

    [Fact]
    public void Validate_DropOnSidebarItem_Network_IsInvalid()
    {
        var result = DragDropValidator.Validate(
            [@"C:\a\file.txt"],
            VirtualPaths.Network,
            isInternal: true,
            sourceDirectory: @"C:\a");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void DriveItem_DropEffect_ExternalIsCopy()
    {
        // Simulates dragging from an external source to a drive
        var result = DragDropValidator.Validate(
            [@"D:\external\file.txt"],
            @"C:\",
            isInternal: false,
            sourceDirectory: null);

        Assert.True(result.IsValid);
        Assert.Equal(DropEffect.Copy, result.Effect);
    }
}
