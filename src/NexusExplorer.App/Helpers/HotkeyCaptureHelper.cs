using Avalonia.Input;

namespace NexusExplorer.App.Helpers;

/// <summary>
/// Builds a canonical shortcut string (e.g. "Ctrl+Alt+E") from Avalonia key events.
/// The produced format matches what the Windows global hotkey service parses.
/// </summary>
public static class HotkeyCaptureHelper
{
    public static string ToShortcut(KeyModifiers modifiers, Key key)
    {
        var parts = new List<string>(5);

        if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");

        parts.Add(ToKeyName(key));
        return string.Join("+", parts);
    }

    public static bool IsModifier(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin;

    private static string ToKeyName(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
            return ((char)('A' + (key - Key.A))).ToString();

        if (key >= Key.D0 && key <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();

        if (key >= Key.F1 && key <= Key.F24)
            return "F" + (1 + (key - Key.F1));

        return key switch
        {
            Key.Space => "Space",
            Key.Enter or Key.Return => "Enter",
            Key.Escape => "Escape",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            _ => key.ToString(),
        };
    }
}
