using System;
using System.Collections.Generic;

namespace Highbyte.DotNet6502;

/// <summary>
/// Helper methods for enum containing instructions.
/// </summary>
public static class OpCodeIdHelpers
{
    public static string ToHex(this OpCodeId value)
    {
        return $"0x{value:X2}";
    }
    public static byte ToByte(this OpCodeId value)
    {
        return (byte)value;
    }
    public static bool Contains(this List<OpCodeId> list, byte opCodeId)
    {
        if(opCodeId.IsDefinedAsOpCodeId())
        {
            return list.Contains(opCodeId.ToOpCodeId());
        }
        return false;
    }
}
