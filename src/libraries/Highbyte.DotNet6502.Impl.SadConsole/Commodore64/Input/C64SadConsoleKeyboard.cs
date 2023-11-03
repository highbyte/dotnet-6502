using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Input;

public class C64SadConsoleKeyboard
{
    public C64SadConsoleKeyboard(string hostKeyboardLayout)
    {
        // TODO: Better way to store/handle different keyboard layouts that hard coded checks
        if (hostKeyboardLayout == "sv")
        {
            foreach (var keyMap in SadConsoleToC64KeyMap_SV_specific)
            {
                SadConsoleToC64KeyMap[keyMap.Key] = keyMap.Value;
            }
        }
        else // Default to US keyboard
        {
            foreach (var keyMap in SadConsoleToC64KeyMap_US_specific)
            {
                SadConsoleToC64KeyMap[keyMap.Key] = keyMap.Value;
            }
        }
    }

    public Dictionary<Keys[], C64Key[]> SadConsoleToC64KeyMap = new()
    {
        { new[] { Keys.Space }, new[] { C64Key.Space } },
        { new[] { Keys.A }, new[] { C64Key.A } },
        { new[] { Keys.B }, new[] { C64Key.B } },
        { new[] { Keys.C }, new[] { C64Key.C } },
        { new[] { Keys.D }, new[] { C64Key.D } },
        { new[] { Keys.E }, new[] { C64Key.E } },
        { new[] { Keys.F }, new[] { C64Key.F } },
        { new[] { Keys.G }, new[] { C64Key.G } },
        { new[] { Keys.H }, new[] { C64Key.H } },
        { new[] { Keys.I }, new[] { C64Key.I } },
        { new[] { Keys.J }, new[] { C64Key.J } },
        { new[] { Keys.K }, new[] { C64Key.K } },
        { new[] { Keys.L }, new[] { C64Key.L } },
        { new[] { Keys.M }, new[] { C64Key.M } },
        { new[] { Keys.N }, new[] { C64Key.N } },
        { new[] { Keys.O }, new[] { C64Key.O } },
        { new[] { Keys.P }, new[] { C64Key.P } },
        { new[] { Keys.Q }, new[] { C64Key.Q } },
        { new[] { Keys.R }, new[] { C64Key.R } },
        { new[] { Keys.S }, new[] { C64Key.S } },
        { new[] { Keys.T }, new[] { C64Key.T } },
        { new[] { Keys.U }, new[] { C64Key.U } },
        { new[] { Keys.V }, new[] { C64Key.V } },
        { new[] { Keys.W }, new[] { C64Key.W } },
        { new[] { Keys.X }, new[] { C64Key.X } },
        { new[] { Keys.Y }, new[] { C64Key.Y } },
        { new[] { Keys.Z }, new[] { C64Key.Z } },
        { new[] { Keys.D0 }, new[] { C64Key.Zero } },
        { new[] { Keys.D1 }, new[] { C64Key.One } },
        { new[] { Keys.D2 }, new[] { C64Key.Two } },
        { new[] { Keys.D3 }, new[] { C64Key.Three } },
        { new[] { Keys.D4 }, new[] { C64Key.Four } },
        { new[] { Keys.D5 }, new[] { C64Key.Five } },
        { new[] { Keys.D6 }, new[] { C64Key.Six } },
        { new[] { Keys.D7 }, new[] { C64Key.Seven } },
        { new[] { Keys.D8 }, new[] { C64Key.Eight } },
        { new[] { Keys.D9 }, new[] { C64Key.Nine } },

        // Non-printable characters

        // First row
        { new[] { Keys.Escape }, new[] { C64Key.Stop } },
        { new[] { Keys.F1 }, new[] { C64Key.F1 } },
        { new[] { Keys.F3 }, new[] { C64Key.F3 } },
        { new[] { Keys.F5 }, new[] { C64Key.F5 } },
        { new[] { Keys.F7 }, new[] { C64Key.F7 } },

        // Navigation
        { new[] { Keys.Insert }, new[] { C64Key.RShift, C64Key.Delete } },
        { new[] { Keys.Home }, new[] { C64Key.Home } },
        // { Key.PageUp, C64Key.Restore }, // TODO: Is not mapped in Keyboard matrix, instead directly to NMI interrupt
        { new[] { Keys.Delete }, new[] { C64Key.Delete }},
        { new[] { Keys.End }, new[] { C64Key.LArrow }},
        { new[] { Keys.PageDown }, new[] { C64Key.UArrow }},
        { new[] { Keys.Down }, new[] { C64Key.CrsrDn } },
        { new[] { Keys.Right }, new[] { C64Key.CrsrRt } },
        { new[] { Keys.Up },  new[] { C64Key.RShift, C64Key.CrsrDn } },
        { new[] { Keys.Left },  new[] { C64Key.RShift, C64Key.CrsrRt } },

        // Misc
        { new[] { Keys.Back }, new[] { C64Key.Delete } },
        { new[] { Keys.Enter }, new[] { C64Key.Return } },
        { new[] { Keys.Tab }, new[] { C64Key.Ctrl } },
        { new[] { Keys.LeftControl }, new[] { C64Key.CBM } },
        { new[] { Keys.LeftShift }, new[] { C64Key.LShift } },
        { new[] { Keys.RightShift }, new[] { C64Key.RShift } },
    };

    public Dictionary<Keys[], C64Key[]> SadConsoleToC64KeyMap_US_specific = new()
    {
    //    // US specific host keyboard characters

        // Numbers row
        // Left of 1: OemTilde
        { new[] { Keys.OemTilde }, new[] { C64Key.RShift, C64Key.Seven } },

        { new[] { Keys.RightShift, Keys.OemTilde }, new[] { C64Key.UArrow } },
        { new[] { Keys.RightShift, Keys.D2 }, new[] {C64Key.At } },
        { new[] { Keys.RightShift, Keys.D6 }, new[] {C64Key.UArrow} },
        { new[] { Keys.RightShift, Keys.D7 }, new[] { C64Key.RShift, C64Key.Six } },
        { new[] { Keys.RightShift, Keys.D8 }, new[] {C64Key.Astrix } },
        { new[] { Keys.RightShift, Keys.D9 }, new[] {C64Key.RShift, C64Key.Eight } },
        { new[] { Keys.RightShift, Keys.D0 }, new[] {C64Key.RShift, C64Key.Nine } },

        // Right of 0: OemMinus, OemPlus
        { new[] { Keys.OemMinus }, new[] { C64Key.Minus } },
        { new[] { Keys.RightShift, Keys.OemMinus }, new[] { C64Key.LArrow } },
        { new[] { Keys.OemPlus }, new[] { C64Key.Equal } },
        { new[] { Keys.RightShift, Keys.OemPlus }, new[] { C64Key.Plus } },

        // Right of P: OemOpenBrackets, OemCloseBrackets, OemBackslash (? not tested)
        { new[] { Keys.OemOpenBrackets }, new[] { C64Key.RShift, C64Key.Colon } },
        { new[] { Keys.RightShift, Keys.OemOpenBrackets }, new[] { C64Key.None } },
        { new[] { Keys.OemCloseBrackets }, new[] { C64Key.RShift, C64Key.Semicol } },
        { new[] { Keys.RightShift, Keys.OemCloseBrackets }, new[] { C64Key.None } },
        { new[] { Keys.OemBackslash }, new[] { C64Key.Lira } },

        // Right of L: OemSemicolon, OemQuotes
        { new[] { Keys.OemSemicolon }, new[] { C64Key.Semicol } },
        { new[] { Keys.RightShift, Keys.OemSemicolon }, new[] { C64Key.Colon } },
        { new[] { Keys.OemQuotes }, new[] { C64Key.RShift, C64Key.Seven } },
        { new[] { Keys.RightShift, Keys.OemQuotes }, new[] { C64Key.RShift, C64Key.Two } },

        // Right of M: OemComma, OemPeriod, OemQuestion
        { new[] { Keys.OemComma }, new[] { C64Key.Comma } },
        //{ new[] { Keys.RightShift, Keys.OemComma }, new[] { C64Key.RShift, C64Key.Comma } },
        { new[] { Keys.OemPeriod }, new[] { C64Key.Period } },
        //{ new[] { Keys.RightShift, Keys.OemPeriod }, new[] { C64Key.RShift, C64Key.Period } },
        { new[] { Keys.OemQuestion }, new[] { C64Key.Slash } },
        //{ new[] { Keys.RightShift, Keys.OemQuestion }, new[] { C64Key.RShift, C64Key.Slash } },

    };

    public Dictionary<Keys[], C64Key[]> SadConsoleToC64KeyMap_SV_specific = new()
    {
        // Swedish specific host keyboard characters

        // Left of 1: None

        // Shift 7 should be /
        { new[] { Keys.RightShift, Keys.D7 }, new[] { C64Key.Slash } },
        // Shift 0 should be =
        { new[] { Keys.RightShift, Keys.D0 }, new[] { C64Key.Equal } },

        // Right of 0: Add, None  (the second key does not register anything in SadConsole)
        // + should be +
        { new[] { Keys.Add }, new[] { C64Key.Plus } },
        // Shift + should be ? 
        { new[] { Keys.RightShift, Keys.Add }, new[] { C64Key.RShift, C64Key.Slash } },
        //// Equal should be ´ 
        //{ new[] { Keys.Equal }, new[] { C64Key.RShift, C64Key.Seven } },
        
        //// Right of P: None, None (SadConsole currently doesn't report anything for None keys)
        //{ new[] { Keys.LeftBracket }, new[] { C64Key.RShift, C64Key.Semicol } },
        //{ new[] { Keys.RightShift, Keys.LeftBracket }, new[] { C64Key.RShift, C64Key.B } },
        //{ new[] { Keys.RightBracket }, new[] { C64Key.RShift, C64Key.Two } },
        //{ new[] { Keys.RightShift, Keys.RightBracket }, new[] { C64Key.RShift, C64Key.UArrow } },

        //// Right of L: None, None, OemQuotes  (SadConsole currently doesn't report anything for the None keys)
        //{ new[] { Keys.Semicolon }, new[] { C64Key.Lira } },
        //{ new[] { Keys.RightShift, Keys.Semicolon }, new[] { C64Key.None } },
        //{ new[] { Keys.Apostrophe }, new[] { C64Key.RShift, C64Key.Colon } },
        //{ new[] { Keys.RightShift, Keys.Apostrophe }, new[] { C64Key.RShift, C64Key.Plus } },
        { new[] { Keys.OemQuotes }, new[] { C64Key.RShift, C64Key.Seven } },
        { new[] { Keys.RightShift, Keys.OemQuotes }, new[] {C64Key.Astrix } },

        // Left of Z: OemBackslash
        { new[] { Keys.OemBackslash }, new[] {C64Key.RShift, C64Key.Comma} },
        { new[] { Keys.LeftShift, Keys.OemBackslash }, new[] {C64Key.RShift, C64Key.Period} },
        { new[] { Keys.RightShift, Keys.OemBackslash }, new[] {C64Key.RShift, C64Key.Period} },

        // Right of M: OemComma, OemPeriod, OemMinus
        { new[] { Keys.OemComma }, new[] {C64Key.Comma } },
        { new[] { Keys.RightShift, Keys.OemComma }, new[] {C64Key.Semicol } },
        { new[] { Keys.OemPeriod }, new[] {C64Key.Period } },
        { new[] { Keys.RightShift, Keys.OemPeriod }, new[] {C64Key.Colon } },
        { new[] { Keys.OemMinus }, new[] { C64Key.Minus } },
        { new[] { Keys.RightShift, Keys.OemMinus }, new[] {C64Key.LArrow } },
    };
}
