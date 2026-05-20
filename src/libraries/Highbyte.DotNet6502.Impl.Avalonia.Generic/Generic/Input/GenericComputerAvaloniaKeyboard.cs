using System.Collections.Generic;
using Avalonia.Input;

namespace Highbyte.DotNet6502.Impl.Avalonia.Generic.Input;

public class GenericComputerAvaloniaKeyboard
{
    // Keyed by PhysicalKey (W3C `code` — key position), matching AvaloniaInputHandlerContext.
    public static Dictionary<PhysicalKey, char> AvaloniaToGenericKeyMap = new()
    {
        { PhysicalKey.Space, ' '},
        { PhysicalKey.Enter, '\n'},

        { PhysicalKey.A, 'a' },
        { PhysicalKey.B, 'b' },
        { PhysicalKey.C, 'c' },
        { PhysicalKey.D, 'd' },
        { PhysicalKey.E, 'e' },
        { PhysicalKey.F, 'f' },
        { PhysicalKey.G, 'g' },
        { PhysicalKey.H, 'h' },
        { PhysicalKey.I, 'i' },
        { PhysicalKey.J, 'j' },
        { PhysicalKey.K, 'k' },
        { PhysicalKey.L, 'l' },
        { PhysicalKey.M, 'm' },
        { PhysicalKey.N, 'n' },
        { PhysicalKey.O, 'o' },
        { PhysicalKey.P, 'p' },
        { PhysicalKey.Q, 'q' },
        { PhysicalKey.R, 'r' },
        { PhysicalKey.S, 's' },
        { PhysicalKey.T, 't' },
        { PhysicalKey.U, 'u' },
        { PhysicalKey.V, 'v' },
        { PhysicalKey.W, 'w' },
        { PhysicalKey.X, 'x' },
        { PhysicalKey.Y, 'y' },
        { PhysicalKey.Z, 'z' },

        { PhysicalKey.Digit0, '0' },
        { PhysicalKey.Digit1, '1' },
        { PhysicalKey.Digit2, '2' },
        { PhysicalKey.Digit3, '3' },
        { PhysicalKey.Digit4, '4' },
        { PhysicalKey.Digit5, '5' },
        { PhysicalKey.Digit6, '6' },
        { PhysicalKey.Digit7, '7' },
        { PhysicalKey.Digit8, '8' },
        { PhysicalKey.Digit9, '9' },
    };
}
