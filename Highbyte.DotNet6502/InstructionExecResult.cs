namespace Highbyte.DotNet6502;

public struct InstructionExecResult
{
    public byte OpCodeByte { get; private set; }
    public bool UnknownInstruction { get; private set; }
    public bool IsBRKInstruction => OpCodeByte == (byte)OpCodeId.BRK;
    public ulong CyclesConsumed { get; private set; }
    public ushort AtPC { get; private set; }

    public InstructionExecResult(byte opCodeByte)
    {
        OpCodeByte = opCodeByte;
        UnknownInstruction = false;
    }

    public static InstructionExecResult UnknownInstructionResult(byte opCodeByte, ushort atPC)
    {
        return new InstructionExecResult(opCodeByte)
        {
            UnknownInstruction = true,
            CyclesConsumed = 1,
            AtPC = atPC
        };
    }

    public static InstructionExecResult KnownInstructionResult(byte opCodeByte, ushort atPC, ulong cyclesConsumed)
    {
        return new InstructionExecResult(opCodeByte)
        {
            UnknownInstruction = false,
            CyclesConsumed = cyclesConsumed,
            AtPC = atPC,
        };
    }
}
