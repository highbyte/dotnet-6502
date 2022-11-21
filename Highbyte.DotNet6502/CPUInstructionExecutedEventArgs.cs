using System;

namespace Highbyte.DotNet6502;

public class CPUInstructionExecutedEventArgs: EventArgs
{
    public CPU CPU { get; }
    public Memory Mem { get; }
    public ExecState InstructionExecState { get; }
    public CPUInstructionExecutedEventArgs(CPU cpu, Memory mem, ExecState instructionExecState)
    {
        CPU = cpu;
        Mem = mem;
        InstructionExecState = instructionExecState;
    }
}
