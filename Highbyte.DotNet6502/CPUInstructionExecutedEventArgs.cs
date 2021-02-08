using System;

namespace Highbyte.DotNet6502
{
    public class CPUInstructionExecutedEventArgs: EventArgs
    {
        public CPU CPU { get; }
        public CPUInstructionExecutedEventArgs(CPU cpu)
        {
            CPU = cpu;
        }
    }
}
