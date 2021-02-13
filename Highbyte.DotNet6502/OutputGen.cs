using System;
using System.IO;

namespace Highbyte.DotNet6502
{
    /// <summary>
    /// Helper class formatting output
    /// </summary>
    public static class OutputGen
    {
        public static string Write(CPU cpu)
        {
            return Write(cpu.PC, cpu.ExecState.LastOpCode.Value);
        }
 
        public static string Write(ushort address, byte opCode, byte[] operand = null)
        {
            string opCodeString;
            if(opCode.IsDefinedAsOpCodeId())
                opCodeString = opCode.ToOpCodeId().ToString();
            else
                opCodeString = opCode.ToHex();
            return $"{address.ToHex()}: {opCodeString}";
        }
    }
}