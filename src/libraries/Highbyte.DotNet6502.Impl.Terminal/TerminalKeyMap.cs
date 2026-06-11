using System.Text;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Impl.Terminal;

internal enum TerminalKeyboardLayout
{
    US,
    Swedish,
}

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
    public static HostKey MapToHostKey(Key key, TerminalKeyboardLayout keyboardLayout)
    {
        // Strip modifier mask bits to get the base key code.
        var code = key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask);

        var special = MapSpecial(code);
        if (special != HostKey.None)
            return special;

        var physical = MapPhysicalKeyCode(code);
        if (physical != HostKey.None)
            return physical;

        // Printable: map from the rune the key produced.
        var rune = key.AsRune;
        if (rune != default)
        {
            return keyboardLayout == TerminalKeyboardLayout.Swedish
                ? MapRuneSwedish(rune)
                : MapRuneUS(rune);
        }

        return HostKey.None;
    }

    public static bool RequiresShift(Key key, TerminalKeyboardLayout keyboardLayout)
    {
        var rune = key.AsRune;
        if (rune == default)
            return false;

        return keyboardLayout == TerminalKeyboardLayout.Swedish
            ? IsShiftedSwedishRune(rune)
            : IsShiftedUsRune(rune);
    }

    public static bool RequiresAltGraph(Key key, TerminalKeyboardLayout keyboardLayout)
    {
        if (keyboardLayout != TerminalKeyboardLayout.Swedish)
            return false;

        var rune = key.AsRune;
        if (rune == default)
            return false;

        return rune.Value is '[' or ']';
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

    private static HostKey MapPhysicalKeyCode(KeyCode code) => code switch
    {
        KeyCode.D0 => HostKey.Digit0,
        KeyCode.D1 => HostKey.Digit1,
        KeyCode.D2 => HostKey.Digit2,
        KeyCode.D3 => HostKey.Digit3,
        KeyCode.D4 => HostKey.Digit4,
        KeyCode.D5 => HostKey.Digit5,
        KeyCode.D6 => HostKey.Digit6,
        KeyCode.D7 => HostKey.Digit7,
        KeyCode.D8 => HostKey.Digit8,
        KeyCode.D9 => HostKey.Digit9,
        KeyCode.A => HostKey.KeyA,
        KeyCode.B => HostKey.KeyB,
        KeyCode.C => HostKey.KeyC,
        KeyCode.D => HostKey.KeyD,
        KeyCode.E => HostKey.KeyE,
        KeyCode.F => HostKey.KeyF,
        KeyCode.G => HostKey.KeyG,
        KeyCode.H => HostKey.KeyH,
        KeyCode.I => HostKey.KeyI,
        KeyCode.J => HostKey.KeyJ,
        KeyCode.K => HostKey.KeyK,
        KeyCode.L => HostKey.KeyL,
        KeyCode.M => HostKey.KeyM,
        KeyCode.N => HostKey.KeyN,
        KeyCode.O => HostKey.KeyO,
        KeyCode.P => HostKey.KeyP,
        KeyCode.Q => HostKey.KeyQ,
        KeyCode.R => HostKey.KeyR,
        KeyCode.S => HostKey.KeyS,
        KeyCode.T => HostKey.KeyT,
        KeyCode.U => HostKey.KeyU,
        KeyCode.V => HostKey.KeyV,
        KeyCode.W => HostKey.KeyW,
        KeyCode.X => HostKey.KeyX,
        KeyCode.Y => HostKey.KeyY,
        KeyCode.Z => HostKey.KeyZ,
        _ => HostKey.None,
    };

    private static HostKey MapRuneUS(Rune rune)
        => MapRuneCommon(rune) switch
        {
            HostKey.None => MapRuneUsPunctuation(rune),
            var hostKey => hostKey,
        };

    private static HostKey MapRuneSwedish(Rune rune)
        => MapRuneCommon(rune) switch
        {
            HostKey.None => MapRuneSwedishPunctuation(rune),
            var hostKey => hostKey,
        };

    private static HostKey MapRuneCommon(Rune rune)
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

        return v == ' ' ? HostKey.Space : HostKey.None;
    }

    private static HostKey MapRuneUsPunctuation(Rune rune)
    {
        var v = rune.Value;
        return v switch
        {
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

    private static HostKey MapRuneSwedishPunctuation(Rune rune)
    {
        var v = rune.Value;

        // Swedish terminal input is delivered as characters, not physical positions. Reverse-map
        // the printable characters that differ from US so the shared C64 Swedish physical-key map
        // can still resolve combinations such as Shift+2 -> '"' and Shift+' -> '*'.
        return v switch
        {
            '"' => HostKey.Digit2,
            '&' => HostKey.Digit6,
            '/' => HostKey.Digit7,
            '(' => HostKey.Digit8,
            ')' => HostKey.Digit9,
            '=' => HostKey.Digit0,
            '+' or '?' => HostKey.Minus,
            '`' or '@' => HostKey.Equal,
            '[' => HostKey.Digit8,
            ']' => HostKey.Digit9,
            '{' => HostKey.BracketLeft,
            '}' => HostKey.BracketRight,
            ';' or ':' => HostKey.Comma,
            '\'' or '*' => HostKey.Quote,
            ',' => HostKey.Comma,
            '.' => HostKey.Period,
            '<' or '>' => HostKey.IntlBackslash,
            '-' or '_' => HostKey.Slash,
            '\\' or '|' => HostKey.Backslash,
            '!' => HostKey.Digit1,
            '#' => HostKey.Digit3,
            '$' => HostKey.Digit4,
            '%' => HostKey.Digit5,
            _ => MapRuneUsPunctuation(rune),
        };
    }

    private static bool IsShiftedUsRune(Rune rune)
    {
        var v = rune.Value;
        return v is '~' or '_' or '+' or '{' or '}' or '|' or ':' or '"' or '<' or '>' or '?'
            or '!' or '@' or '#' or '$' or '%' or '^' or '&' or '*' or '(' or ')';
    }

    private static bool IsShiftedSwedishRune(Rune rune)
    {
        var v = rune.Value;
        return v is '"' or '&' or '/' or '(' or ')' or '=' or '?' or '*' or ';' or ':' or '>';
    }

    // offset 0 == 'A'
    private static HostKey LetterHostKey(int offsetFromA) =>
        (HostKey)((int)HostKey.KeyA + offsetFromA);
}
