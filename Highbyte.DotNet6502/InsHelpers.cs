using System;
using System.Collections.Generic;

namespace Highbyte.DotNet6502
{
    /// <summary>
    /// Helper methods for enum containing instructions.
    /// </summary>
    public static class InsHelpers
    {
        public static string ToHex(this Ins value)
        {
            return $"0x{value:X2}";
        }
        public static byte ToByte(this Ins value)
        {
            return (byte)value;
        }
        public static bool Contains(this List<Ins> list, byte ins)
        {
            if(ins.IsDefined())
            {
                return list.Contains(ins.ToIns());
            }
            return false;
        }
   }
}
