using System.Text;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Impl.Terminal;

/// <summary>
/// Maps Terminal.Gui <see cref="Key"/> values to the neutral physical <see cref="HostKey"/>
/// abstraction. Special/navigation keys are mapped from <see cref="KeyCode"/>; printable keys are
/// mapped from the produced rune (a-z, 0-9 and common punctuation).
///
/// Because terminals deliver characters rather than physical key codes, this mapping is by
/// necessity approximate: it recovers the physical key for the common BASIC/typing set, but cannot
/// distinguish every shifted symbol's physical origin. Unmapped keys return <see cref="HostKey.None"/>.
/// </summary>
internal static class TerminalKeyMap
{
    public static HostKey MapToHostKey(Key key)
    {
        // Strip modifier mask bits to get the base key code.
        var code = key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask);

        var special = MapSpecial(code);
        if (special != HostKey.None)
            return special;

        // Printable: map from the rune the key produced.
        var rune = key.AsRune;
        if (rune != default)
            return MapRune(rune);

        return HostKey.None;
    }

    private static HostKey MapSpecial(KeyCode code) => code switch
    {
        KeyCode.Enter => HostKey.Enter,
        KeyCode.Space => HostKey.Space,
        KeyCode.Tab => HostKey.Tab,
        KeyCode.Backspace => HostKey.Backspace,
        KeyCode.Delete => HostKey.Delete,
        KeyCode.Insert => HostKey.Insert,
        KeyCode.Esc => HostKey.Escape,
        KeyCode.Home => HostKey.Home,
        KeyCode.End => HostKey.End,
        KeyCode.PageUp => HostKey.PageUp,
        KeyCode.PageDown => HostKey.PageDown,
        KeyCode.CursorUp => HostKey.ArrowUp,
        KeyCode.CursorDown => HostKey.ArrowDown,
        KeyCode.CursorLeft => HostKey.ArrowLeft,
        KeyCode.CursorRight => HostKey.ArrowRight,
        KeyCode.F1 => HostKey.F1,
        KeyCode.F2 => HostKey.F2,
        KeyCode.F3 => HostKey.F3,
        KeyCode.F4 => HostKey.F4,
        KeyCode.F5 => HostKey.F5,
        KeyCode.F6 => HostKey.F6,
        KeyCode.F7 => HostKey.F7,
        KeyCode.F8 => HostKey.F8,
        KeyCode.F9 => HostKey.F9,
        KeyCode.F10 => HostKey.F10,
        KeyCode.F11 => HostKey.F11,
        KeyCode.F12 => HostKey.F12,
        _ => HostKey.None,
    };

    private static HostKey MapRune(Rune rune)
    {
        var v = rune.Value;
        // Letters (map upper- and lower-case to the same physical key).
        if (v is >= 'a' and <= 'z')
            return LetterHostKey((char)(v - 'a'));
        if (v is >= 'A' and <= 'Z')
            return LetterHostKey((char)(v - 'A'));
        // Digits on the main row.
        if (v is >= '0' and <= '9')
            return (HostKey)((int)HostKey.Digit0 + (v - '0'));

        return v switch
        {
            ' ' => HostKey.Space,
            '`' or '~' => HostKey.Backquote,
            '-' or '_' => HostKey.Minus,
            '=' or '+' => HostKey.Equal,
            '[' or '{' => HostKey.BracketLeft,
            ']' or '}' => HostKey.BracketRight,
            '\\' or '|' => HostKey.Backslash,
            ';' or ':' => HostKey.Semicolon,
            '\'' or '"' => HostKey.Quote,
            ',' or '<' => HostKey.Comma,
            '.' or '>' => HostKey.Period,
            '/' or '?' => HostKey.Slash,
            // Shifted digit symbols on a US layout — map back to the physical digit key.
            '!' => HostKey.Digit1,
            '@' => HostKey.Digit2,
            '#' => HostKey.Digit3,
            '$' => HostKey.Digit4,
            '%' => HostKey.Digit5,
            '^' => HostKey.Digit6,
            '&' => HostKey.Digit7,
            '*' => HostKey.Digit8,
            '(' => HostKey.Digit9,
            ')' => HostKey.Digit0,
            _ => HostKey.None,
        };
    }

    // offset 0 == 'A'
    private static HostKey LetterHostKey(int offsetFromA) =>
        (HostKey)((int)HostKey.KeyA + offsetFromA);
}
