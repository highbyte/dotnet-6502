using System;

namespace Highbyte.DotNet6502
{
    public class CPUInstructionToBeExecutedEventArgs: EventArgs
    {
        public CPU CPU { get; }
        public CPUInstructionToBeExecutedEventArgs(CPU cpu)
        {
            CPU = cpu;
        }
    }
}
