using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Commodore64.Input;

/// <summary>
/// The C64 keyboard mapping: host <see cref="HostKey"/> combinations to C64 keyboard-matrix keys.
///
/// This is the single, host-agnostic replacement for the formerly duplicated per-host keyboard
/// classes (<c>C64SilkNetKeyboard</c>, <c>C64AvaloniaKeyboard</c>, <c>C64SadConsoleKeyboard</c>,
/// <c>C64AspNetKeyboard</c>). Each host now only translates its native key type into
/// <see cref="HostKey"/>; the actual C64 mapping lives here once.
///
/// The map keys are <see cref="HostKey"/> arrays — a mapping matches when every host key in the
/// array is currently held down. Entries are matched by reference (each array instance is a
/// distinct dictionary key), mirroring the original per-host design.
/// </summary>
public class C64HostKeyboard
{
    /// <summary>The host keyboard layout this map was built for.</summary>
    public C64KeyboardLayout Layout { get; }

    /// <summary>
    /// Builds the keyboard map for the given host keyboard layout.
    /// </summary>
    /// <param name="hostKeyboardLayout">The host physical keyboard layout to build the map for.</param>
    public C64HostKeyboard(C64KeyboardLayout hostKeyboardLayout)
    {
        Layout = hostKeyboardLayout;
        var layoutSpecific = hostKeyboardLayout == C64KeyboardLayout.Swedish
            ? HostKeyToC64KeyMap_SV_specific
            : HostKeyToC64KeyMap_US_specific;

        foreach (var keyMap in layoutSpecific)
            HostKeyToC64KeyMap[keyMap.Key] = keyMap.Value;
    }

    /// <summary>
    /// The complete map for the selected layout (layout-independent base entries plus the
    /// layout-specific entries merged in by the constructor).
    /// </summary>
    public Dictionary<HostKey[], C64Key[]> HostKeyToC64KeyMap = new()
    {
        { new[] { HostKey.Space }, new[] { C64Key.Space } },
        { new[] { HostKey.KeyA }, new[] { C64Key.A } },
        { new[] { HostKey.KeyB }, new[] { C64Key.B } },
        { new[] { HostKey.KeyC }, new[] { C64Key.C } },
        { new[] { HostKey.KeyD }, new[] { C64Key.D } },
        { new[] { HostKey.KeyE }, new[] { C64Key.E } },
        { new[] { HostKey.KeyF }, new[] { C64Key.F } },
        { new[] { HostKey.KeyG }, new[] { C64Key.G } },
        { new[] { HostKey.KeyH }, new[] { C64Key.H } },
        { new[] { HostKey.KeyI }, new[] { C64Key.I } },
        { new[] { HostKey.KeyJ }, new[] { C64Key.J } },
        { new[] { HostKey.KeyK }, new[] { C64Key.K } },
        { new[] { HostKey.KeyL }, new[] { C64Key.L } },
        { new[] { HostKey.KeyM }, new[] { C64Key.M } },
        { new[] { HostKey.KeyN }, new[] { C64Key.N } },
        { new[] { HostKey.KeyO }, new[] { C64Key.O } },
        { new[] { HostKey.KeyP }, new[] { C64Key.P } },
        { new[] { HostKey.KeyQ }, new[] { C64Key.Q } },
        { new[] { HostKey.KeyR }, new[] { C64Key.R } },
        { new[] { HostKey.KeyS }, new[] { C64Key.S } },
        { new[] { HostKey.KeyT }, new[] { C64Key.T } },
        { new[] { HostKey.KeyU }, new[] { C64Key.U } },
        { new[] { HostKey.KeyV }, new[] { C64Key.V } },
        { new[] { HostKey.KeyW }, new[] { C64Key.W } },
        { new[] { HostKey.KeyX }, new[] { C64Key.X } },
        { new[] { HostKey.KeyY }, new[] { C64Key.Y } },
        { new[] { HostKey.KeyZ }, new[] { C64Key.Z } },
        { new[] { HostKey.Digit0 }, new[] { C64Key.Zero } },
        { new[] { HostKey.Digit1 }, new[] { C64Key.One } },
        { new[] { HostKey.Digit2 }, new[] { C64Key.Two } },
        { new[] { HostKey.Digit3 }, new[] { C64Key.Three } },
        { new[] { HostKey.Digit4 }, new[] { C64Key.Four } },
        { new[] { HostKey.Digit5 }, new[] { C64Key.Five } },
        { new[] { HostKey.Digit6 }, new[] { C64Key.Six } },
        { new[] { HostKey.Digit7 }, new[] { C64Key.Seven } },
        { new[] { HostKey.Digit8 }, new[] { C64Key.Eight } },
        { new[] { HostKey.Digit9 }, new[] { C64Key.Nine } },

        // Non-printable characters

        // First row
        { new[] { HostKey.Escape }, new[] { C64Key.Stop } },
        { new[] { HostKey.F1 }, new[] { C64Key.F1 } },
        { new[] { HostKey.F3 }, new[] { C64Key.F3 } },
        { new[] { HostKey.F5 }, new[] { C64Key.F5 } },
        { new[] { HostKey.F7 }, new[] { C64Key.F7 } },

        // Navigation
        { new[] { HostKey.Insert }, new[] { C64Key.RShift, C64Key.Delete } },
        { new[] { HostKey.Home }, new[] { C64Key.Home } },
        // PageUp -> C64 Restore: not in the keyboard matrix, wired directly to NMI (handled separately).
        { new[] { HostKey.Delete }, new[] { C64Key.Delete } },
        { new[] { HostKey.End }, new[] { C64Key.LArrow } },
        { new[] { HostKey.PageDown }, new[] { C64Key.UArrow } },
        { new[] { HostKey.ArrowDown }, new[] { C64Key.CrsrDn } },
        { new[] { HostKey.ArrowRight }, new[] { C64Key.CrsrRt } },
        { new[] { HostKey.ArrowUp }, new[] { C64Key.RShift, C64Key.CrsrDn } },
        { new[] { HostKey.ArrowLeft }, new[] { C64Key.RShift, C64Key.CrsrRt } },

        // Misc
        { new[] { HostKey.Backspace }, new[] { C64Key.Delete } },
        { new[] { HostKey.Enter }, new[] { C64Key.Return } },
        { new[] { HostKey.Tab }, new[] { C64Key.Ctrl } },
        { new[] { HostKey.ControlLeft }, new[] { C64Key.CBM } },
        { new[] { HostKey.ShiftLeft }, new[] { C64Key.LShift } },
        { new[] { HostKey.ShiftRight }, new[] { C64Key.RShift } },
    };

    public Dictionary<HostKey[], C64Key[]> HostKeyToC64KeyMap_US_specific = new()
    {
        // US specific host keyboard characters

        // Left of 1: Backquote
        { new[] { HostKey.Backquote }, new[] { C64Key.RShift, C64Key.Seven } },
        { new[] { HostKey.ShiftRight, HostKey.Backquote }, new[] { C64Key.UArrow } },

        // Numbers row
        { new[] { HostKey.ShiftRight, HostKey.Digit2 }, new[] { C64Key.At } },
        { new[] { HostKey.ShiftRight, HostKey.Digit6 }, new[] { C64Key.UArrow } },
        { new[] { HostKey.ShiftRight, HostKey.Digit7 }, new[] { C64Key.RShift, C64Key.Six } },
        { new[] { HostKey.ShiftRight, HostKey.Digit8 }, new[] { C64Key.Astrix } },
        { new[] { HostKey.ShiftRight, HostKey.Digit9 }, new[] { C64Key.RShift, C64Key.Eight } },
        { new[] { HostKey.ShiftRight, HostKey.Digit0 }, new[] { C64Key.RShift, C64Key.Nine } },

        // Right of 0: Minus, Equal
        { new[] { HostKey.Minus }, new[] { C64Key.Minus } },
        { new[] { HostKey.ShiftRight, HostKey.Minus }, new[] { C64Key.LArrow } },
        { new[] { HostKey.Equal }, new[] { C64Key.Equal } },
        { new[] { HostKey.ShiftRight, HostKey.Equal }, new[] { C64Key.Plus } },

        // Right of P: BracketLeft, BracketRight, Backslash
        { new[] { HostKey.BracketLeft }, new[] { C64Key.RShift, C64Key.Colon } },
        { new[] { HostKey.ShiftRight, HostKey.BracketLeft }, new[] { C64Key.None } },
        { new[] { HostKey.BracketRight }, new[] { C64Key.RShift, C64Key.Semicol } },
        { new[] { HostKey.ShiftRight, HostKey.BracketRight }, new[] { C64Key.None } },
        { new[] { HostKey.Backslash }, new[] { C64Key.Lira } },

        // Right of L: Semicolon, Quote
        { new[] { HostKey.Semicolon }, new[] { C64Key.Semicol } },
        { new[] { HostKey.ShiftRight, HostKey.Semicolon }, new[] { C64Key.Colon } },
        { new[] { HostKey.Quote }, new[] { C64Key.RShift, C64Key.Seven } },
        { new[] { HostKey.ShiftRight, HostKey.Quote }, new[] { C64Key.RShift, C64Key.Two } },

        // Right of M: Comma, Period, Slash
        { new[] { HostKey.Comma }, new[] { C64Key.Comma } },
        { new[] { HostKey.Period }, new[] { C64Key.Period } },
        { new[] { HostKey.Slash }, new[] { C64Key.Slash } },
    };

    public Dictionary<HostKey[], C64Key[]> HostKeyToC64KeyMap_SV_specific = new()
    {
        // Swedish specific host keyboard characters

        // Alt(Option)+8 -> [, Alt(Option)+9 -> ]
        // On a Swedish keyboard the "[" and "]" characters are produced by Option+8 / Option+9 on
        // Mac (and AltGr+8 / AltGr+9 on Windows/Linux). Bound additively in addition to the
        // existing Ä -> [ and Å -> ] convenience mappings further down. The chord wins over the
        // base Digit8/Digit9 mapping via the C64InputHandler conflict-resolution rule
        // (more-specific entries displace less-specific ones that share keys).
        { new[] { HostKey.AltLeft, HostKey.Digit8 }, new[] { C64Key.RShift, C64Key.Colon } },
        { new[] { HostKey.AltRight, HostKey.Digit8 }, new[] { C64Key.RShift, C64Key.Colon } },
        { new[] { HostKey.AltLeft, HostKey.Digit9 }, new[] { C64Key.RShift, C64Key.Semicol } },
        { new[] { HostKey.AltRight, HostKey.Digit9 }, new[] { C64Key.RShift, C64Key.Semicol } },

        // Numbers row
        // Shift 7 should be /
        { new[] { HostKey.ShiftRight, HostKey.Digit7 }, new[] { C64Key.Slash } },
        // Shift 0 should be =
        { new[] { HostKey.ShiftRight, HostKey.Digit0 }, new[] { C64Key.Equal } },

        // Right of 0: Minus, Equal
        // - should be +
        { new[] { HostKey.Minus }, new[] { C64Key.Plus } },
        // Shift - (which is + for sv) should be ? (which is / for sv)
        { new[] { HostKey.ShiftRight, HostKey.Minus }, new[] { C64Key.RShift, C64Key.Slash } },
        // Equal should be ´
        { new[] { HostKey.Equal }, new[] { C64Key.RShift, C64Key.Seven } },

        // Right of P: BracketLeft, BracketRight
        { new[] { HostKey.BracketLeft }, new[] { C64Key.RShift, C64Key.Semicol } },
        { new[] { HostKey.ShiftRight, HostKey.BracketLeft }, new[] { C64Key.RShift, C64Key.B } },
        { new[] { HostKey.BracketRight }, new[] { C64Key.RShift, C64Key.Two } },
        { new[] { HostKey.ShiftRight, HostKey.BracketRight }, new[] { C64Key.RShift, C64Key.UArrow } },

        // Right of L: Semicolon, Quote, Backslash
        { new[] { HostKey.Semicolon }, new[] { C64Key.Lira } },
        { new[] { HostKey.ShiftRight, HostKey.Semicolon }, new[] { C64Key.None } },
        { new[] { HostKey.Quote }, new[] { C64Key.RShift, C64Key.Colon } },
        // Shifted Quote on Swedish (host prints '*') -> C64 dedicated asterisk key.
        // Also helps SadConsole/MonoGame on Swedish, where the '/' key is reported
        // as HostKey.Quote rather than HostKey.Backslash (MonoGame's Keys.OemQuotes
        // is layout-bound, not positional).
        { new[] { HostKey.ShiftRight, HostKey.Quote }, new[] { C64Key.Astrix } },
        { new[] { HostKey.Backslash }, new[] { C64Key.RShift, C64Key.Seven } },
        { new[] { HostKey.ShiftRight, HostKey.Backslash }, new[] { C64Key.Astrix } },

        // Left of Z: IntlBackslash
        { new[] { HostKey.IntlBackslash }, new[] { C64Key.RShift, C64Key.Comma } },
        { new[] { HostKey.ShiftLeft, HostKey.IntlBackslash }, new[] { C64Key.RShift, C64Key.Period } },
        { new[] { HostKey.ShiftRight, HostKey.IntlBackslash }, new[] { C64Key.RShift, C64Key.Period } },

        // Right of M: Comma, Period, Slash
        { new[] { HostKey.Comma }, new[] { C64Key.Comma } },
        { new[] { HostKey.ShiftRight, HostKey.Comma }, new[] { C64Key.Semicol } },
        { new[] { HostKey.Period }, new[] { C64Key.Period } },
        { new[] { HostKey.ShiftRight, HostKey.Period }, new[] { C64Key.Colon } },
        { new[] { HostKey.Slash }, new[] { C64Key.Minus } },
        { new[] { HostKey.ShiftRight, HostKey.Slash }, new[] { C64Key.LArrow } },
    };
}
