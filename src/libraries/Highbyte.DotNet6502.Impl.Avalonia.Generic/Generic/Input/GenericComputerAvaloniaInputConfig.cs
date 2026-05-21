using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace Highbyte.DotNet6502.Impl.Avalonia.Generic.Input;

public class GenericComputerAvaloniaInputConfig : ICloneable
{
    // Map Avalonia keys to generic computer input
    public Dictionary<Key, byte> KeyToValueMap = new()
    {
        { Key.Up, 0x01 },
        { Key.Down, 0x02 },
        { Key.Left, 0x04 },
        { Key.Right, 0x08 },
        { Key.Space, 0x10 },
        { Key.Enter, 0x20 },
        { Key.W, 0x01 },
        { Key.S, 0x02 },
        { Key.A, 0x04 },
        { Key.D, 0x08 },
    };

    public object Clone()
    {
        var clone = new GenericComputerAvaloniaInputConfig
        {
            KeyToValueMap = new Dictionary<Key, byte>(KeyToValueMap)
        };

        return clone;
    }
}
