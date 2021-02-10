using System.Collections.Generic;

namespace Highbyte.DotNet6502
{
    public static class Data
    {
        public static byte[] GetResetVectorCode(ushort userCodeAddress)
        {
            List<byte> list = new()
            {
                // TODO: Add init code for setting SP, I flag, etc, as a normal 6502 would do in the reset vector
                // End with jumping to address where our actual user code lives. Typically this would be the address of Basic.
                (byte)OpCodeId.JMP_ABS
            };
            List<byte> code = list;
            code.AddRange(userCodeAddress.ToLittleEndianBytes()); 
            return code.ToArray();
        }
    }
}
