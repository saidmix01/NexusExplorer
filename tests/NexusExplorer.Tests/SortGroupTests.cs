using NexusExplorer.Core.Models;
using NexusExplorer.App.ViewModels;

namespace NexusExplorer.Tests;

public class SortGroupTests
{
    [Fact]
    public void FileSortMode_Enum_HasExpectedValues()
    {
        var values = Enum.GetValues<FileSortMode>();
        Assert.Contains(FileSortMode.Name, values);
        Assert.Contains(FileSortMode.DateModified, values);
        Assert.Contains(FileSortMode.Type, values);
        Assert.Contains(FileSortMode.Size, values);
    }

    [Fact]
    public void FileGroupMode_Enum_HasExpectedValues()
    {
        var values = Enum.GetValues<FileGroupMode>();
        Assert.Contains(FileGroupMode.None, values);
        Assert.Contains(FileGroupMode.DateModified, values);
        Assert.Contains(FileGroupMode.Type, values);
    }

    [Fact]
    public void SortDirection_Enum_HasExpectedValues()
    {
        var values = Enum.GetValues<SortDirection>();
        Assert.Contains(SortDirection.Ascending, values);
        Assert.Contains(SortDirection.Descending, values);
    }
}
