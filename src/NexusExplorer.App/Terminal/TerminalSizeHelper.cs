namespace NexusExplorer.App.Terminal;

/// <summary>
/// Static helper for calculating terminal grid dimensions from pixel sizes.
/// </summary>
public static class TerminalSizeHelper
{
    /// <summary>
    /// Calculates the number of columns that fit in a given pixel width.
    /// Returns at least 1.
    /// </summary>
    public static int CalculateColumns(double widthPixels, double cellWidth)
    {
        if (cellWidth <= 0 || widthPixels <= 0)
            return 1;
        return Math.Max(1, (int)(widthPixels / cellWidth));
    }

    /// <summary>
    /// Calculates the number of rows that fit in a given pixel height.
    /// Returns at least 1.
    /// </summary>
    public static int CalculateRows(double heightPixels, double cellHeight)
    {
        if (cellHeight <= 0 || heightPixels <= 0)
            return 1;
        return Math.Max(1, (int)(heightPixels / cellHeight));
    }
}
