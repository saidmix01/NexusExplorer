using NexusExplorer.App.Terminal;

namespace NexusExplorer.Tests;

public class TerminalSizeHelperTests
{
    // Typical cell dimensions for Cascadia Mono 14pt: approx 8.4 x 18.5
    private const double TypicalCellWidth = 8.4;
    private const double TypicalCellHeight = 18.5;

    [Fact]
    public void CalculateColumns_NormalSize_ReturnsExpected()
    {
        // 800px / 8.4 ≈ 95 columns
        var cols = TerminalSizeHelper.CalculateColumns(800, TypicalCellWidth);
        Assert.Equal(95, cols);
    }

    [Fact]
    public void CalculateRows_NormalSize_ReturnsExpected()
    {
        // 400px / 18.5 ≈ 21 rows
        var rows = TerminalSizeHelper.CalculateRows(400, TypicalCellHeight);
        Assert.Equal(21, rows);
    }

    [Fact]
    public void CalculateColumns_SmallSize_ReturnsAtLeastOne()
    {
        // 5px / 8.4 < 1, but minimum is 1
        var cols = TerminalSizeHelper.CalculateColumns(5, TypicalCellWidth);
        Assert.Equal(1, cols);
    }

    [Fact]
    public void CalculateRows_SmallSize_ReturnsAtLeastOne()
    {
        // 10px / 18.5 < 1, but minimum is 1
        var rows = TerminalSizeHelper.CalculateRows(10, TypicalCellHeight);
        Assert.Equal(1, rows);
    }

    [Fact]
    public void CalculateColumns_LargeSize_ReturnsLargeValue()
    {
        // 1920px / 8.4 ≈ 228
        var cols = TerminalSizeHelper.CalculateColumns(1920, TypicalCellWidth);
        Assert.Equal(228, cols);
    }

    [Fact]
    public void CalculateRows_LargeSize_ReturnsLargeValue()
    {
        // 1080px / 18.5 ≈ 58
        var rows = TerminalSizeHelper.CalculateRows(1080, TypicalCellHeight);
        Assert.Equal(58, rows);
    }

    [Fact]
    public void CalculateColumns_ZeroWidth_ReturnsOne()
    {
        var cols = TerminalSizeHelper.CalculateColumns(0, TypicalCellWidth);
        Assert.Equal(1, cols);
    }

    [Fact]
    public void CalculateRows_ZeroHeight_ReturnsOne()
    {
        var rows = TerminalSizeHelper.CalculateRows(0, TypicalCellHeight);
        Assert.Equal(1, rows);
    }

    [Fact]
    public void CalculateColumns_NegativeWidth_ReturnsOne()
    {
        var cols = TerminalSizeHelper.CalculateColumns(-100, TypicalCellWidth);
        Assert.Equal(1, cols);
    }

    [Fact]
    public void CalculateRows_NegativeHeight_ReturnsOne()
    {
        var rows = TerminalSizeHelper.CalculateRows(-50, TypicalCellHeight);
        Assert.Equal(1, rows);
    }

    [Fact]
    public void CalculateColumns_ZeroCellWidth_ReturnsOne()
    {
        var cols = TerminalSizeHelper.CalculateColumns(800, 0);
        Assert.Equal(1, cols);
    }

    [Fact]
    public void CalculateRows_ZeroCellHeight_ReturnsOne()
    {
        var rows = TerminalSizeHelper.CalculateRows(400, 0);
        Assert.Equal(1, rows);
    }

    [Fact]
    public void CalculateColumns_NegativeCellWidth_ReturnsOne()
    {
        var cols = TerminalSizeHelper.CalculateColumns(800, -8.4);
        Assert.Equal(1, cols);
    }

    [Fact]
    public void CalculateRows_NegativeCellHeight_ReturnsOne()
    {
        var rows = TerminalSizeHelper.CalculateRows(400, -18.5);
        Assert.Equal(1, rows);
    }

    [Fact]
    public void CalculateColumns_ExactMultiple_ReturnsExact()
    {
        // 84px / 8.4 = exactly 10
        var cols = TerminalSizeHelper.CalculateColumns(84, 8.4);
        Assert.Equal(10, cols);
    }

    [Fact]
    public void CalculateRows_ExactMultiple_ReturnsExact()
    {
        // 185px / 18.5 = exactly 10
        var rows = TerminalSizeHelper.CalculateRows(185, 18.5);
        Assert.Equal(10, rows);
    }

    [Fact]
    public void CalculateColumns_LessThanOneCell_ReturnsOne()
    {
        // Width is less than one cell width
        var cols = TerminalSizeHelper.CalculateColumns(4, TypicalCellWidth);
        Assert.Equal(1, cols);
    }

    [Fact]
    public void CalculateRows_LessThanOneCell_ReturnsOne()
    {
        // Height is less than one cell height
        var rows = TerminalSizeHelper.CalculateRows(10, TypicalCellHeight);
        Assert.Equal(1, rows);
    }
}
