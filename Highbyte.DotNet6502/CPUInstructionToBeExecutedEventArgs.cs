using System;

namespace Highbyte.DotNet6502;

public class CPUInstructionToBeExecutedEventArgs: EventArgs
{
    public CPU CPU { get; }
    public Memory Mem { get; }
    public CPUInstructionToBeExecutedEventArgs(CPU cpu, Memory mem)
    {
        CPU = cpu;
        Mem = mem;            
    }
}
