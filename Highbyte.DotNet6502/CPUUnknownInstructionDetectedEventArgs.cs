using System;

namespace Highbyte.DotNet6502
{
    public class CPUUnknownOpCodeDetectedEventArgs: EventArgs
    {
        public CPU CPU { get; }
        public byte OpCode { get; }

        public CPUUnknownOpCodeDetectedEventArgs(CPU cpu, byte opCode)
        {
            CPU = cpu;
            OpCode = opCode;
        }
    }
}