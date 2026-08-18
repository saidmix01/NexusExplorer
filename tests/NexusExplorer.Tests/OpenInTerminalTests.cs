using NexusExplorer.App.Helpers;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Tests;

public class OpenInTerminalTests
{
    private static FileSystemItem MakeDir(string path) => new()
    {
        Name = Path.GetFileName(path),
        Path = path,
        Type = FileSystemItemType.Directory
    };

    private static FileSystemItem MakeFile(string path) => new()
    {
        Name = Path.GetFileName(path),
        Path = path,
        Type = FileSystemItemType.File,
        Size = 100
    };

    [Fact]
    public void Directory_ReturnsDirectoryPath()
    {
        var item = MakeDir(@"C:\Projects\App");
        var result = TerminalPathHelper.GetTerminalWorkingDirectory(item, @"C:\Projects");

        Assert.Equal(@"C:\Projects\App", result);
    }

    [Fact]
    public void File_ReturnsParentDirectory()
    {
        var item = MakeFile(@"C:\Projects\App\Program.cs");
        var result = TerminalPathHelper.GetTerminalWorkingDirectory(item, @"C:\Projects");

        Assert.Equal(@"C:\Projects\App", result);
    }

    [Fact]
    public void NullItem_ReturnsCurrentPath()
    {
        var result = TerminalPathHelper.GetTerminalWorkingDirectory(null, @"C:\Users\Said\Documents");

        Assert.Equal(@"C:\Users\Said\Documents", result);
    }

    [Fact]
    public void NullItem_EmptyCurrentPath_ReturnsNull()
    {
        var result = TerminalPathHelper.GetTerminalWorkingDirectory(null, "");

        Assert.Null(result);
    }

    [Fact]
    public void NullItem_WhitespaceCurrentPath_ReturnsNull()
    {
        var result = TerminalPathHelper.GetTerminalWorkingDirectory(null, "   ");

        Assert.Null(result);
    }

    [Fact]
    public void FileAtRoot_ReturnsRootDirectory()
    {
        var item = MakeFile(@"C:\file.txt");
        var result = TerminalPathHelper.GetTerminalWorkingDirectory(item, @"C:\");

        Assert.Equal(@"C:\", result);
    }

    [Fact]
    public void Directory_DeepPath_ReturnsFullPath()
    {
        var item = MakeDir(@"C:\Users\Said\Documents\Projects\NexusExplorer\src");
        var result = TerminalPathHelper.GetTerminalWorkingDirectory(item, @"C:\");

        Assert.Equal(@"C:\Users\Said\Documents\Projects\NexusExplorer\src", result);
    }

    [Fact]
    public void File_InNestedDirectory_ReturnsParent()
    {
        var item = MakeFile(@"C:\Users\Said\Documents\report.pdf");
        var result = TerminalPathHelper.GetTerminalWorkingDirectory(item, @"C:\");

        Assert.Equal(@"C:\Users\Said\Documents", result);
    }
}
