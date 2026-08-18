using Avalonia.Media;

namespace NexusExplorer.App.Terminal;

/// <summary>
/// Defines the color palette and appearance settings for the terminal.
/// Supports the standard 16 ANSI colors (normal + bright).
/// </summary>
public sealed class TerminalTheme
{
    public required string Name { get; init; }
    public required Color Background { get; init; }
    public required Color Foreground { get; init; }
    public required Color CursorColor { get; init; }
    public required Color SelectionBackground { get; init; }
    public required Color SelectionForeground { get; init; }

    /// <summary>
    /// 16 ANSI colors: indices 0-7 = normal, 8-15 = bright.
    /// </summary>
    public required Color[] AnsiColors { get; init; }

    public Color GetAnsiColor(int index, bool bright = false)
    {
        var i = bright ? index + 8 : index;
        if (i >= 0 && i < AnsiColors.Length)
            return AnsiColors[i];
        return Foreground;
    }

    // --- Built-in themes ---

    public static TerminalTheme LinuxDark { get; } = new()
    {
        Name = "Linux Dark",
        Background = Color.Parse("#0C0C0C"),
        Foreground = Color.Parse("#D4D4D4"),
        CursorColor = Color.Parse("#D4D4D4"),
        SelectionBackground = Color.Parse("#264F78"),
        SelectionForeground = Color.Parse("#FFFFFF"),
        AnsiColors =
        [
            Color.Parse("#0C0C0C"), // 0  Black
            Color.Parse("#C50F1F"), // 1  Red
            Color.Parse("#13A10E"), // 2  Green
            Color.Parse("#C19C00"), // 3  Yellow
            Color.Parse("#0037DA"), // 4  Blue
            Color.Parse("#881798"), // 5  Magenta
            Color.Parse("#3A96DD"), // 6  Cyan
            Color.Parse("#CCCCCC"), // 7  White
            Color.Parse("#767676"), // 8  Bright Black
            Color.Parse("#E74856"), // 9  Bright Red
            Color.Parse("#16C60C"), // 10 Bright Green
            Color.Parse("#F9F1A5"), // 11 Bright Yellow
            Color.Parse("#3B78FF"), // 12 Bright Blue
            Color.Parse("#B4009E"), // 13 Bright Magenta
            Color.Parse("#61D6D6"), // 14 Bright Cyan
            Color.Parse("#F2F2F2"), // 15 Bright White
        ]
    };

    public static TerminalTheme WindowsTerminalDark { get; } = new()
    {
        Name = "Windows Terminal Dark",
        Background = Color.Parse("#012456"),
        Foreground = Color.Parse("#CCCCCC"),
        CursorColor = Color.Parse("#FFFFFF"),
        SelectionBackground = Color.Parse("#3A6EA5"),
        SelectionForeground = Color.Parse("#FFFFFF"),
        AnsiColors =
        [
            Color.Parse("#0C0C0C"), // 0  Black
            Color.Parse("#C50F1F"), // 1  Red
            Color.Parse("#13A10E"), // 2  Green
            Color.Parse("#C19C00"), // 3  Yellow
            Color.Parse("#0037DA"), // 4  Blue
            Color.Parse("#881798"), // 5  Magenta
            Color.Parse("#3A96DD"), // 6  Cyan
            Color.Parse("#CCCCCC"), // 7  White
            Color.Parse("#767676"), // 8  Bright Black
            Color.Parse("#E74856"), // 9  Bright Red
            Color.Parse("#16C60C"), // 10 Bright Green
            Color.Parse("#F9F1A5"), // 11 Bright Yellow
            Color.Parse("#3B78FF"), // 12 Bright Blue
            Color.Parse("#B4009E"), // 13 Bright Magenta
            Color.Parse("#61D6D6"), // 14 Bright Cyan
            Color.Parse("#F2F2F2"), // 15 Bright White
        ]
    };
}
