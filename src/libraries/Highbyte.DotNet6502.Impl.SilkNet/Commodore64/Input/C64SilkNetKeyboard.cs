using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input;

public class C64SilkNetKeyboard
{
    public C64SilkNetKeyboard(string hostKeyboardLayout)
    {
        // TODO: Better way to store/handle different keyboard layouts that hard coded checks
        if (hostKeyboardLayout == "sv")
        {
            foreach (var keyMap in SilkNetToC64KeyMap_SV_specific)
            {
                SilkNetToC64KeyMap[keyMap.Key] = keyMap.Value;
            }
        }
        else // Default to US keyboard
        {
            foreach (var keyMap in SilkNetToC64KeyMap_US_specific)
            {
                SilkNetToC64KeyMap[keyMap.Key] = keyMap.Value;
            }
        }
    }

    public Dictionary<Key[], C64Key[]> SilkNetToC64KeyMap = new()
    {
        { new[] { Key.Space }, new[] { C64Key.Space } },
        { new[] { Key.A }, new[] { C64Key.A } },
        { new[] { Key.B }, new[] { C64Key.B } },
        { new[] { Key.C }, new[] { C64Key.C } },
        { new[] { Key.D }, new[] { C64Key.D } },
        { new[] { Key.E }, new[] { C64Key.E } },
        { new[] { Key.F }, new[] { C64Key.F } },
        { new[] { Key.G }, new[] { C64Key.G } },
        { new[] { Key.H }, new[] { C64Key.H } },
        { new[] { Key.I }, new[] { C64Key.I } },
        { new[] { Key.J }, new[] { C64Key.J } },
        { new[] { Key.K }, new[] { C64Key.K } },
        { new[] { Key.L }, new[] { C64Key.L } },
        { new[] { Key.M }, new[] { C64Key.M } },
        { new[] { Key.N }, new[] { C64Key.N } },
        { new[] { Key.O }, new[] { C64Key.O } },
        { new[] { Key.P }, new[] { C64Key.P } },
        { new[] { Key.Q }, new[] { C64Key.Q } },
        { new[] { Key.R }, new[] { C64Key.R } },
        { new[] { Key.S }, new[] { C64Key.S } },
        { new[] { Key.T }, new[] { C64Key.T } },
        { new[] { Key.U }, new[] { C64Key.U } },
        { new[] { Key.V }, new[] { C64Key.V } },
        { new[] { Key.W }, new[] { C64Key.W } },
        { new[] { Key.X }, new[] { C64Key.X } },
        { new[] { Key.Y }, new[] { C64Key.Y } },
        { new[] { Key.Z }, new[] { C64Key.Z } },
        { new[] { Key.Number0 }, new[] { C64Key.Zero } },
        { new[] { Key.Number1 }, new[] { C64Key.One } },
        { new[] { Key.Number2 }, new[] { C64Key.Two } },
        { new[] { Key.Number3 }, new[] { C64Key.Three } },
        { new[] { Key.Number4 }, new[] { C64Key.Four } },
        { new[] { Key.Number5 }, new[] { C64Key.Five } },
        { new[] { Key.Number6 }, new[] { C64Key.Six } },
        { new[] { Key.Number7 }, new[] { C64Key.Seven } },
        { new[] { Key.Number8 }, new[] { C64Key.Eight } },
        { new[] { Key.Number9 }, new[] { C64Key.Nine } },

        // Non-printable characters

        // First row
        { new[] { Key.Escape }, new[] { C64Key.Stop } },
        { new[] { Key.F1 }, new[] { C64Key.F1 } },
        { new[] { Key.F3 }, new[] { C64Key.F3 } },
        { new[] { Key.F5 }, new[] { C64Key.F5 } },
        { new[] { Key.F7 }, new[] { C64Key.F7 } },

        // Navigation
        { new[] { Key.Insert }, new[] { C64Key.RShift, C64Key.Delete } },
        { new[] { Key.Home }, new[] { C64Key.Home } },
        // { Key.PageUp, C64Key.Restore }, // TODO: Is not mapped in Keyboard matrix, instead directly to NMI interrupt
        { new[] { Key.Delete }, new[] { C64Key.Delete }},
        { new[] { Key.End }, new[] { C64Key.LArrow }},
        { new[] { Key.PageDown }, new[] { C64Key.UArrow }},
        { new[] { Key.Down }, new[] { C64Key.CrsrDn } },
        { new[] { Key.Right }, new[] { C64Key.CrsrRt } },
        { new[] { Key.Up },  new[] { C64Key.RShift, C64Key.CrsrDn } },
        { new[] { Key.Left },  new[] { C64Key.RShift, C64Key.CrsrRt } },

        // Misc
        { new[] { Key.Backspace }, new[] { C64Key.Delete } },
        { new[] { Key.Enter }, new[] { C64Key.Return } },
        { new[] { Key.Tab }, new[] { C64Key.CBM } },
        { new[] { Key.ControlLeft }, new[] { C64Key.Ctrl } },
        { new[] { Key.ControlRight }, new[] { C64Key.Ctrl } },
        { new[] { Key.ShiftLeft }, new[] { C64Key.LShift } },
        { new[] { Key.ShiftRight }, new[] { C64Key.RShift } },
    };

    public Dictionary<Key[], C64Key[]> SilkNetToC64KeyMap_US_specific = new()
    {
        // US specific host keyboard characters

        // Left of 1: GraveAccent
        { new[] { Key.GraveAccent }, new[] { C64Key.RShift, C64Key.Seven } },
        { new[] { Key.ShiftRight, Key.GraveAccent }, new[] { C64Key.UArrow } },

        // Numbers row
        { new[] { Key.ShiftRight, Key.Number2 }, new[] {C64Key.At } },
        { new[] { Key.ShiftRight, Key.Number6 }, new[] {C64Key.UArrow} },
        { new[] { Key.ShiftRight, Key.Number7 }, new[] { C64Key.RShift, C64Key.Six } },
        { new[] { Key.ShiftRight, Key.Number8 }, new[] {C64Key.Astrix } },
        { new[] { Key.ShiftRight, Key.Number9 }, new[] {C64Key.RShift, C64Key.Eight } },
        { new[] { Key.ShiftRight, Key.Number0 }, new[] {C64Key.RShift, C64Key.Nine } },

        // Right of 0: Minus, Equal
        { new[] { Key.Minus }, new[] { C64Key.Minus } },
        { new[] { Key.ShiftRight, Key.Minus }, new[] { C64Key.LArrow } },
        { new[] { Key.Equal }, new[] { C64Key.Equal } },
        { new[] { Key.ShiftRight, Key.Equal }, new[] { C64Key.Plus } },

        // Right of P: LeftBracket, RightBracket
        { new[] { Key.LeftBracket }, new[] { C64Key.RShift, C64Key.Colon } },
        { new[] { Key.ShiftRight, Key.LeftBracket }, new[] { C64Key.None } },
        { new[] { Key.RightBracket }, new[] { C64Key.RShift, C64Key.Semicol } },
        { new[] { Key.ShiftRight, Key.RightBracket }, new[] { C64Key.None } },
        { new[] { Key.BackSlash }, new[] { C64Key.Lira } },

        // Right of L: Semicolon, Apostrophe 
        { new[] { Key.Semicolon }, new[] { C64Key.Semicol } },
        { new[] { Key.ShiftRight, Key.Semicolon }, new[] { C64Key.Colon } },
        { new[] { Key.Apostrophe }, new[] { C64Key.RShift, C64Key.Seven } },
        { new[] { Key.ShiftRight, Key.Apostrophe }, new[] { C64Key.RShift, C64Key.Two } },

        // Right of M: Comma, Period, Slash
        { new[] { Key.Comma }, new[] { C64Key.Comma } },
        //{ new[] { Key.ShiftRight, Key.Comma }, new[] { C64Key.RShift, C64Key.Comma } },
        { new[] { Key.Period }, new[] { C64Key.Period } },
        //{ new[] { Key.ShiftRight, Key.Period }, new[] { C64Key.RShift, C64Key.Period } },
        { new[] { Key.Slash }, new[] { C64Key.Slash } },
        //{ new[] { Key.ShiftRight, Key.Slash }, new[] { C64Key.RShift, C64Key.Slash } },

    };

    public Dictionary<Key[], C64Key[]> SilkNetToC64KeyMap_SV_specific = new()
    {
        // Swedish specific host keyboard characters

        // Numbers row
        // Shift 7 should be /
        { new[] { Key.ShiftRight, Key.Number7}, new[] { C64Key.Slash } },
        // Shift 0 should be =
        { new[] { Key.ShiftRight, Key.Number0}, new[] { C64Key.Equal } },

        // Right of 0: Minus, Equal
        // - should be +
        { new[] { Key.Minus }, new[] { C64Key.Plus } },
        // Shift - (which is + for sv) should be ? (which is / for sv)
        { new[] { Key.ShiftRight, Key.Minus }, new[] { C64Key.RShift, C64Key.Slash } },
        // Equal should be ´
        { new[] { Key.Equal }, new[] { C64Key.RShift, C64Key.Seven } },

        // Right of P: LeftBracket, RightBracket
        { new[] { Key.LeftBracket }, new[] { C64Key.RShift, C64Key.Semicol } },
        { new[] { Key.ShiftRight, Key.LeftBracket }, new[] { C64Key.RShift, C64Key.B } },
        { new[] { Key.RightBracket }, new[] { C64Key.RShift, C64Key.Two } },
        { new[] { Key.ShiftRight, Key.RightBracket }, new[] { C64Key.RShift, C64Key.UArrow } },

        // Right of L: Semicolon, Apostrophe, BackSlash, 
        { new[] { Key.Semicolon }, new[] { C64Key.Lira } },
        { new[] { Key.ShiftRight, Key.Semicolon }, new[] { C64Key.None } },
        { new[] { Key.Apostrophe }, new[] { C64Key.RShift, C64Key.Colon } },
        { new[] { Key.ShiftRight, Key.Apostrophe }, new[] { C64Key.RShift, C64Key.Plus } },
        { new[] { Key.BackSlash }, new[] { C64Key.RShift, C64Key.Seven } },
        { new[] { Key.ShiftRight, Key.BackSlash }, new[] {C64Key.Astrix } },

        // Left of Z: World2
        { new[] { Key.World2 }, new[] {C64Key.RShift, C64Key.Comma} },
        { new[] { Key.ShiftLeft, Key.World2 }, new[] {C64Key.RShift, C64Key.Period} },
        { new[] { Key.ShiftRight, Key.World2 }, new[] {C64Key.RShift, C64Key.Period} },

        // Right of M: Comma, Period, Slash
        { new[] { Key.Comma }, new[] {C64Key.Comma } },
        { new[] { Key.ShiftRight, Key.Comma }, new[] {C64Key.Semicol } },
        { new[] { Key.Period }, new[] {C64Key.Period } },
        { new[] { Key.ShiftRight, Key.Period }, new[] {C64Key.Colon } },
        { new[] { Key.Slash }, new[] { C64Key.Minus } },
        { new[] { Key.ShiftRight, Key.Slash }, new[] {C64Key.LArrow } },
    };

}
