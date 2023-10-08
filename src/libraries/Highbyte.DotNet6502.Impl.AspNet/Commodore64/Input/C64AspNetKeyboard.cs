using Highbyte.DotNet6502.Instructions;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;

public class C64AspNetKeyboard
{
    public C64AspNetKeyboard(string hostKeyboardLayout)
    {
        // TODO: Better way to store/handle different keyboard layouts that hard coded checks
        if (hostKeyboardLayout == "sv")
        {
            foreach (var keyMap in AspNetToC64KeyMap_SV_specific)
            {
                AspNetToC64KeyMap[keyMap.Key] = keyMap.Value;
            }
        }
        else // Default to US keyboard
        {
            //foreach (var keyMap in AspNetToC64KeyMap_US_specific)
            //{
            //    SilkNetToC64KeyMap[keyMap.Key] = keyMap.Value;
            //}
        }
    }

    public Dictionary<string[], C64Key[]> AspNetToC64KeyMap = new()
    {
        { new[] { "Space" }, new[] { C64Key.Space } },
        { new[] { "KeyA" }, new[] { C64Key.A } },
        { new[] { "KeyB" }, new[] { C64Key.B } },
        { new[] { "KeyC" }, new[] { C64Key.C } },
        { new[] { "KeyD" }, new[] { C64Key.D } },
        { new[] { "KeyE" }, new[] { C64Key.E } },
        { new[] { "KeyF" }, new[] { C64Key.F } },
        { new[] { "KeyG" }, new[] { C64Key.G } },
        { new[] { "KeyH" }, new[] { C64Key.H } },
        { new[] { "KeyI" }, new[] { C64Key.I } },
        { new[] { "KeyJ" }, new[] { C64Key.J } },
        { new[] { "KeyK" }, new[] { C64Key.K } },
        { new[] { "KeyL" }, new[] { C64Key.L } },
        { new[] { "KeyM" }, new[] { C64Key.M } },
        { new[] { "KeyN" }, new[] { C64Key.N } },
        { new[] { "KeyO" }, new[] { C64Key.O } },
        { new[] { "KeyP" }, new[] { C64Key.P } },
        { new[] { "KeyQ" }, new[] { C64Key.Q } },
        { new[] { "KeyR" }, new[] { C64Key.R } },
        { new[] { "KeyS" }, new[] { C64Key.S } },
        { new[] { "KeyT" }, new[] { C64Key.T } },
        { new[] { "KeyU" }, new[] { C64Key.U } },
        { new[] { "KeyV" }, new[] { C64Key.V } },
        { new[] { "KeyW" }, new[] { C64Key.W } },
        { new[] { "KeyX" }, new[] { C64Key.X } },
        { new[] { "KeyY" }, new[] { C64Key.Y } },
        { new[] { "KeyZ" }, new[] { C64Key.Z } },
        { new[] { "Key0" }, new[] { C64Key.Zero } },
        { new[] { "Digit1" }, new[] { C64Key.One } },
        { new[] { "Digit2" }, new[] { C64Key.Two } },
        { new[] { "Digit3" }, new[] { C64Key.Three } },
        { new[] { "Digit4" }, new[] { C64Key.Four } },
        { new[] { "Digit5" }, new[] { C64Key.Five } },
        { new[] { "Digit6" }, new[] { C64Key.Six } },
        { new[] { "Digit7" }, new[] { C64Key.Seven } },
        { new[] { "Digit8" }, new[] { C64Key.Eight } },
        { new[] { "Digit9" }, new[] { C64Key.Nine } },

        // Non-printable characters

        // First row
        { new[] { "Escape" }, new[] { C64Key.Stop } },
        { new[] { "F1" }, new[] { C64Key.F1 } },
        { new[] { "F3" }, new[] { C64Key.F3 } },
        { new[] { "F5" }, new[] { C64Key.F5 } },
        { new[] { "F7" }, new[] { C64Key.F7 } },

        // Navigation
        { new[] { "Insert" }, new[] { C64Key.RShift, C64Key.Delete } },
        { new[] { "Home" }, new[] { C64Key.Home } },
        //// { Key.PageUp, C64Key.Restore }, // TODO: Is not mapped in Keyboard matrix, instead directly to NMI interrupt
        { new[] { "Delete" }, new[] { C64Key.Delete }},
        { new[] { "End" }, new[] { C64Key.LArrow }},
        { new[] { "PageDown" }, new[] { C64Key.UArrow }},
        { new[] { "ArrowDown" }, new[] { C64Key.CrsrDn } },
        { new[] { "ArrowRight" }, new[] { C64Key.CrsrRt } },
        { new[] { "ArrowUp" },  new[] { C64Key.RShift, C64Key.CrsrDn } },
        { new[] { "ArrowLeft" },  new[] { C64Key.RShift, C64Key.CrsrRt } },

        // Misc
        { new[] { "Backspace" }, new[] { C64Key.Delete } },
        { new[] { "Enter" }, new[] { C64Key.Return } },
        { new[] { "Tab" }, new[] { C64Key.CBM } },
        { new[] { "ControlLeft" }, new[] { C64Key.Ctrl } },
        { new[] { "ControlRight" }, new[] { C64Key.Ctrl } },
        { new[] { "ShiftLeft" }, new[] { C64Key.LShift } },
        { new[] { "ShiftRight" }, new[] { C64Key.RShift } },
    };

    //public Dictionary<Key[], C64Key[]> SilkNetToC64KeyMap_US_specific = new()
    //{
    //    // US specific host keyboard characters

    //    // Numbers row
    //    { new[] { Key.GraveAccent }, new[] { C64Key.RShift, C64Key.Seven } },
    //    { new[] { Key.ShiftRight, Key.GraveAccent }, new[] { C64Key.UArrow } },
    //    { new[] { Key.ShiftRight, Key.Number2 }, new[] {C64Key.At } },
    //    { new[] { Key.ShiftRight, Key.Number6 }, new[] {C64Key.UArrow} },
    //    { new[] { Key.ShiftRight, Key.Number7 }, new[] { C64Key.RShift, C64Key.Six } },
    //    { new[] { Key.ShiftRight, Key.Number8 }, new[] {C64Key.Astrix } },
    //    { new[] { Key.ShiftRight, Key.Number9 }, new[] {C64Key.RShift, C64Key.Eight } },
    //    { new[] { Key.ShiftRight, Key.Number0 }, new[] {C64Key.RShift, C64Key.Nine } },
    //    { new[] { Key.Minus }, new[] { C64Key.Minus } },
    //    { new[] { Key.ShiftRight, Key.Minus }, new[] { C64Key.LArrow } },
    //    { new[] { Key.Equal }, new[] { C64Key.Equal } },
    //    { new[] { Key.ShiftRight, Key.Equal }, new[] { C64Key.Plus } },

    //    // Right of P: LeftBracket, RightBracket
    //    { new[] { Key.LeftBracket }, new[] { C64Key.RShift, C64Key.Colon } },
    //    { new[] { Key.ShiftRight, Key.LeftBracket }, new[] { C64Key.None } },
    //    { new[] { Key.RightBracket }, new[] { C64Key.RShift, C64Key.Semicol } },
    //    { new[] { Key.ShiftRight, Key.RightBracket }, new[] { C64Key.None } },
    //    { new[] { Key.BackSlash }, new[] { C64Key.Lira } },

    //    // Right of L: Semicolon, Apostrophe 
    //    { new[] { Key.Semicolon }, new[] { C64Key.Semicol } },
    //    { new[] { Key.ShiftRight, Key.Semicolon }, new[] { C64Key.Colon } },
    //    { new[] { Key.Apostrophe }, new[] { C64Key.RShift, C64Key.Seven } },
    //    { new[] { Key.ShiftRight, Key.Apostrophe }, new[] { C64Key.RShift, C64Key.Two } },

    //    // Right of M: Comma, Period, Slash
    //    { new[] { Key.Comma }, new[] { C64Key.Comma } },
    //    //{ new[] { Key.ShiftRight, Key.Comma }, new[] { C64Key.RShift, C64Key.Comma } },
    //    { new[] { Key.Period }, new[] { C64Key.Period } },
    //    //{ new[] { Key.ShiftRight, Key.Period }, new[] { C64Key.RShift, C64Key.Period } },
    //    { new[] { Key.Slash }, new[] { C64Key.Slash } },
    //    //{ new[] { Key.ShiftRight, Key.Slash }, new[] { C64Key.RShift, C64Key.Slash } },

    //};

    public Dictionary<string[], C64Key[]> AspNetToC64KeyMap_SV_specific = new()
    {
        // Swedish specific host keyboard characters

        // Numbers row
        // Shift 7 should be /
        { new[] { "ShiftRight", "Digit7" }, new[] { C64Key.Slash } },
        // Shift 0 should be =
        { new[] { "ShiftRight", "Digit0" }, new[] { C64Key.Equal } },
        // - should be +
        { new[] { "Minus" }, new[] { C64Key.Plus } },
        // Shift - (which is + for sv) should be ? (which is / for sv)
        { new[] { "ShiftRight", "Minus" }, new[] { C64Key.RShift, C64Key.Slash } },
        // Equal should be ´
        { new[] { "Equal" }, new[] { C64Key.RShift, C64Key.Seven } },

        // Right of P: BracketLeft, BracketRight
        { new[] { "BracketLeft" }, new[] { C64Key.RShift, C64Key.Semicol } },
        { new[] { "ShiftRight", "BracketLeft" }, new[] { C64Key.RShift, C64Key.B } },
        { new[] { "BracketRight" }, new[] { C64Key.RShift, C64Key.Two } },
        { new[] { "ShiftRight", "BracketRight" }, new[] { C64Key.RShift, C64Key.UArrow } },

        // Right of L: Semicolon, Quote, Backslash, 
        { new[] { "Semicolon" }, new[] { C64Key.Lira } },
        { new[] { "ShiftRight", "Semicolon" }, new[] { C64Key.None } },
        { new[] { "Quote" }, new[] { C64Key.RShift, C64Key.Colon } },
        { new[] { "ShiftRight", "Quote" }, new[] { C64Key.RShift, C64Key.Plus } },
        { new[] { "Backslash" }, new[] { C64Key.RShift, C64Key.Seven } },
        { new[] { "ShiftRight", "Backslash" }, new[] {C64Key.Astrix } },

        // Left of M: IntlBackslash
        { new[] { "IntlBackslash" }, new[] {C64Key.RShift, C64Key.Comma} },
        { new[] { "ShiftLeft", "IntlBackslash" }, new[] {C64Key.RShift, C64Key.Period} },
        { new[] { "ShiftRight", "IntlBackslash" }, new[] {C64Key.RShift, C64Key.Period} },

        // Right of M: Comma, Period, Slash
        { new[] { "Comma" }, new[] {C64Key.Comma } },
        { new[] { "ShiftRight", "Comma" }, new[] {C64Key.Semicol } },
        { new[] { "Period" }, new[] {C64Key.Period } },
        { new[] { "ShiftRight", "Period" }, new[] {C64Key.Colon } },
        { new[] { "Slash" }, new[] { C64Key.Minus } },
        { new[] { "ShiftRight", "Slash" }, new[] {C64Key.LArrow } },
    };
}
