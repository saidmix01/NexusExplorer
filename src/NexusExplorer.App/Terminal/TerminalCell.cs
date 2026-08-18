namespace NexusExplorer.App.Terminal;

/// <summary>
/// Represents a single cell in the terminal grid with character and display attributes.
/// </summary>
public struct TerminalCell
{
    public char Character;
    public byte ForegroundIndex; // 0-15 ANSI index, 255 = default
    public byte BackgroundIndex; // 0-15 ANSI index, 255 = default
    public CellAttributes Attributes;

    public static TerminalCell Empty => new()
    {
        Character = ' ',
        ForegroundIndex = 255,
        BackgroundIndex = 255,
        Attributes = CellAttributes.None
    };
}

[Flags]
public enum CellAttributes : byte
{
    None = 0,
    Bold = 1,
    Underline = 2,
    Inverse = 4,
    Dim = 8,
    Italic = 16
}
