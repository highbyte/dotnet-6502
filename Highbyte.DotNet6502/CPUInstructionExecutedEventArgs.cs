using System;

namespace Highbyte.DotNet6502
{
    public class CPUInstructionExecutedEventArgs: EventArgs
    {
        public CPU CPU { get; }
        public Memory Mem { get; }
        public CPUInstructionExecutedEventArgs(CPU cpu, Memory mem)
        {
            CPU = cpu;
            Mem = mem;
        }
    }
}
