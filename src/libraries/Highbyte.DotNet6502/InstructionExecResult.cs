namespace Highbyte.DotNet6502;

public struct InstructionExecResult
{
    public byte OpCodeByte { get; private set; }
    public bool UnknownInstruction { get; private set; }
    /// <summary>
    /// True when this result was produced by an actual instruction execution or a
    /// deliberate pre-execution peek (i.e. not a default zero-initialised instance).
    /// Evaluators that inspect <see cref="OpCodeByte"/> or <see cref="IsBRKInstruction"/>
    /// should check <see cref="IsValid"/> first to avoid false positives caused by the
    /// fact that the default value of <see cref="OpCodeByte"/> (0x00) equals the BRK opcode.
    /// </summary>
    public bool IsValid { get; private set; }
    public bool IsBRKInstruction => IsValid && OpCodeByte == (byte)OpCodeId.BRK;
    public ulong CyclesConsumed { get; private set; }
    public ushort AtPC { get; private set; }

    public InstructionExecResult(byte opCodeByte)
    {
        OpCodeByte = opCodeByte;
        UnknownInstruction = false;
        IsValid = false; // only factory methods produce valid results
    }

    public static InstructionExecResult UnknownInstructionResult(byte opCodeByte, ushort atPC)
    {
        return new InstructionExecResult(opCodeByte)
        {
            UnknownInstruction = true,
            IsValid = true,
            CyclesConsumed = 1,
            AtPC = atPC
        };
    }

    public static InstructionExecResult KnownInstructionResult(byte opCodeByte, ushort atPC, ulong cyclesConsumed)
    {
        return new InstructionExecResult(opCodeByte)
        {
            UnknownInstruction = false,
            IsValid = true,
            CyclesConsumed = cyclesConsumed,
            AtPC = atPC,
        };
    }
}
