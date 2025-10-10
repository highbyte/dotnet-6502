using System.Collections.Generic;
using Avalonia.Input;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Input;

public class C64AvaloniaKeyboard
{
    public C64AvaloniaKeyboard(string hostKeyboardLayout)
    {
        // TODO: Better way to store/handle different keyboard layouts that hard coded checks
        if (hostKeyboardLayout == "sv")
        {
            foreach (var keyMap in AvaloniaToC64KeyMap_SV_specific)
            {
                AvaloniaToC64KeyMap[keyMap.Key] = keyMap.Value;
            }
        }
        else // Default to US keyboard
        {
            foreach (var keyMap in AvaloniaToC64KeyMap_US_specific)
            {
                AvaloniaToC64KeyMap[keyMap.Key] = keyMap.Value;
            }
        }
    }

    public Dictionary<Key[], C64Key[]> AvaloniaToC64KeyMap = new()
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
        { new[] { Key.D0 }, new[] { C64Key.Zero } },
        { new[] { Key.D1 }, new[] { C64Key.One } },
        { new[] { Key.D2 }, new[] { C64Key.Two } },
        { new[] { Key.D3 }, new[] { C64Key.Three } },
        { new[] { Key.D4 }, new[] { C64Key.Four } },
        { new[] { Key.D5 }, new[] { C64Key.Five } },
        { new[] { Key.D6 }, new[] { C64Key.Six } },
        { new[] { Key.D7 }, new[] { C64Key.Seven } },
        { new[] { Key.D8 }, new[] { C64Key.Eight } },
        { new[] { Key.D9 }, new[] { C64Key.Nine } },

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
        { new[] { Key.Back }, new[] { C64Key.Delete } },
        { new[] { Key.Enter }, new[] { C64Key.Return } },
        { new[] { Key.Tab }, new[] { C64Key.Ctrl } },
        { new[] { Key.LeftCtrl }, new[] { C64Key.CBM } },
        { new[] { Key.LeftShift }, new[] { C64Key.LShift } },
        { new[] { Key.RightShift }, new[] { C64Key.RShift } },
    };

    public Dictionary<Key[], C64Key[]> AvaloniaToC64KeyMap_US_specific = new()
    {
        // US specific host keyboard characters

        // Left of 1: GraveAccent
        { new[] { Key.OemTilde }, new[] { C64Key.LArrow } },

        // Special characters
        { new[] { Key.OemMinus }, new[] { C64Key.Plus } },
        { new[] { Key.OemPlus }, new[] { C64Key.Minus } },
        { new[] { Key.OemOpenBrackets }, new[] { C64Key.At } },
        { new[] { Key.OemCloseBrackets }, new[] { C64Key.Astrix } },
        { new[] { Key.OemPipe }, new[] { C64Key.UArrow } },
        { new[] { Key.OemSemicolon }, new[] { C64Key.Colon } },
        { new[] { Key.OemQuotes }, new[] { C64Key.Semicol } },
        { new[] { Key.OemComma }, new[] { C64Key.Comma } },
        { new[] { Key.OemPeriod }, new[] { C64Key.Period } },
        { new[] { Key.OemQuestion }, new[] { C64Key.Slash } },

        // Shifted characters (combinations)
        { new[] { Key.LeftShift, Key.D1 }, new[] { C64Key.LShift, C64Key.One } },     // !
        { new[] { Key.LeftShift, Key.D2 }, new[] { C64Key.LShift, C64Key.Two } },     // @  (but C64 @ is different key)
        { new[] { Key.LeftShift, Key.D3 }, new[] { C64Key.LShift, C64Key.Three } },   // #
        { new[] { Key.LeftShift, Key.D4 }, new[] { C64Key.LShift, C64Key.Four } },    // $
        { new[] { Key.LeftShift, Key.D5 }, new[] { C64Key.LShift, C64Key.Five } },    // %
        { new[] { Key.LeftShift, Key.D6 }, new[] { C64Key.LShift, C64Key.Six } },     // ^  (but C64 ^ is different key)
        { new[] { Key.LeftShift, Key.D7 }, new[] { C64Key.LShift, C64Key.Seven } },   // &
        { new[] { Key.LeftShift, Key.D8 }, new[] { C64Key.LShift, C64Key.Eight } },   // *  (but C64 * is different key)
        { new[] { Key.LeftShift, Key.D9 }, new[] { C64Key.LShift, C64Key.Nine } },    // (
        { new[] { Key.LeftShift, Key.D0 }, new[] { C64Key.LShift, C64Key.Zero } },    // )

        { new[] { Key.RightShift, Key.D1 }, new[] { C64Key.RShift, C64Key.One } },     // !
        { new[] { Key.RightShift, Key.D2 }, new[] { C64Key.RShift, C64Key.Two } },     // @  (but C64 @ is different key)
        { new[] { Key.RightShift, Key.D3 }, new[] { C64Key.RShift, C64Key.Three } },   // #
        { new[] { Key.RightShift, Key.D4 }, new[] { C64Key.RShift, C64Key.Four } },    // $
        { new[] { Key.RightShift, Key.D5 }, new[] { C64Key.RShift, C64Key.Five } },    // %
        { new[] { Key.RightShift, Key.D6 }, new[] { C64Key.RShift, C64Key.Six } },     // ^  (but C64 ^ is different key)
        { new[] { Key.RightShift, Key.D7 }, new[] { C64Key.RShift, C64Key.Seven } },   // &
        { new[] { Key.RightShift, Key.D8 }, new[] { C64Key.RShift, C64Key.Eight } },   // *  (but C64 * is different key)
        { new[] { Key.RightShift, Key.D9 }, new[] { C64Key.RShift, C64Key.Nine } },    // (
        { new[] { Key.RightShift, Key.D0 }, new[] { C64Key.RShift, C64Key.Zero } },    // )
    };

    public Dictionary<Key[], C64Key[]> AvaloniaToC64KeyMap_SV_specific = new()
    {
        // Swedish specific host keyboard characters
        
        // Left of 1: Section symbol
        { new[] { Key.OemTilde }, new[] { C64Key.LArrow } },

        // Special characters adjusted for Swedish layout
        { new[] { Key.OemPlus }, new[] { C64Key.Plus } },
        { new[] { Key.OemQuestion }, new[] { C64Key.Minus } },
        { new[] { Key.OemOpenBrackets }, new[] { C64Key.At } },
        { new[] { Key.OemCloseBrackets }, new[] { C64Key.Astrix } },
        { new[] { Key.OemPipe }, new[] { C64Key.UArrow } },
        { new[] { Key.OemSemicolon }, new[] { C64Key.Colon } },
        { new[] { Key.OemQuotes }, new[] { C64Key.Semicol } },
        { new[] { Key.OemComma }, new[] { C64Key.Comma } },
        { new[] { Key.OemPeriod }, new[] { C64Key.Period } },
        { new[] { Key.OemMinus }, new[] { C64Key.Slash } },
    };
}
