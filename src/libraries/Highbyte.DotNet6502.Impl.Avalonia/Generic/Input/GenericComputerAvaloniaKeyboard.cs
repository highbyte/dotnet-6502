using System.Collections.Generic;
using Avalonia.Input;

namespace Highbyte.DotNet6502.Impl.Avalonia.Generic.Input;

public class GenericComputerAvaloniaKeyboard
{
    public static Dictionary<Key, char> AvaloniaToGenericKeyMap = new()
    {
        { Key.Space, ' '},
        { Key.Return, '\n'},

        { Key.A, 'a' },
        { Key.B, 'b' },
        { Key.C, 'c' },
        { Key.D, 'd' },
        { Key.E, 'e' },
        { Key.F, 'f' },
        { Key.G, 'g' },
        { Key.H, 'h' },
        { Key.I, 'i' },
        { Key.J, 'j' },
        { Key.K, 'k' },
        { Key.L, 'l' },
        { Key.M, 'm' },
        { Key.N, 'n' },
        { Key.O, 'o' },
        { Key.P, 'p' },
        { Key.Q, 'q' },
        { Key.R, 'r' },
        { Key.S, 's' },
        { Key.T, 't' },
        { Key.U, 'u' },
        { Key.V, 'v' },
        { Key.W, 'w' },
        { Key.X, 'x' },
        { Key.Y, 'y' },
        { Key.Z, 'z' },

        { Key.D0, '0' },
        { Key.D1, '1' },
        { Key.D2, '2' },
        { Key.D3, '3' },
        { Key.D4, '4' },
        { Key.D5, '5' },
        { Key.D6, '6' },
        { Key.D7, '7' },
        { Key.D8, '8' },
        { Key.D9, '9' },
    };
}
