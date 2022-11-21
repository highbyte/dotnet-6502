using System;

namespace Highbyte.DotNet6502;

public class CPUUnknownOpCodeDetectedEventArgs: EventArgs
{
    public CPU CPU { get; }
    public Memory Mem { get; }
    public byte OpCode { get; }

    public CPUUnknownOpCodeDetectedEventArgs(CPU cpu, Memory mem, byte opCode)
    {
        CPU = cpu;
        Mem = mem;            
        OpCode = opCode;
    }
}