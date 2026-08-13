namespace Highbyte.DotNet6502;

public struct InstructionExecResult
{
    public byte OpCodeByte { get; private set; }
    public bool UnknownInstruction { get; private set; }
    public bool HaltedCpu { get; private set; }
    /// <summary>
    /// True when this result was produced by an actual instruction execution or a
    /// deliberate pre-execution peek (i.e. not a default zero-initialised instance).
    /// Evaluators that inspect <see cref="OpCodeByte"/> or <see cref="IsBRKInstruction"/>
    /// should check <see cref="IsValid"/> first to avoid false positives caused by the
    /// fact that the default value of <see cref="OpCodeByte"/> (0x00) equals the BRK opcode.
    /// </summary>
    public bool IsValid { get; private set; }
    public bool IsBRKInstruction => IsValid && OpCodeByte == (byte)OpCodeId.BRK;
    public bool IsHaltInstruction => IsValid && HaltedCpu;
    public ulong CyclesConsumed { get; private set; }
    public ushort AtPC { get; private set; }

    /// <summary>
    /// Returns a copy with additional cycles added to <see cref="CyclesConsumed"/>.
    /// Used to fold the hardware interrupt-entry cost (<see cref="CPU.InterruptEntryCycles"/>)
    /// serviced at the instruction boundary into the preceding instruction's result, so
    /// cycle-paced consumers (device ticking, frame budgets, statistics) see real elapsed time.
    /// </summary>
    public InstructionExecResult WithAdditionalCycles(ulong additionalCycles)
    {
        var copy = this;
        copy.CyclesConsumed += additionalCycles;
        return copy;
    }

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
            HaltedCpu = false,
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
            HaltedCpu = false,
            IsValid = true,
            CyclesConsumed = cyclesConsumed,
            AtPC = atPC,
        };
    }

    public static InstructionExecResult HaltInstructionResult(byte opCodeByte, ushort atPC, ulong cyclesConsumed)
    {
        return new InstructionExecResult(opCodeByte)
        {
            UnknownInstruction = false,
            HaltedCpu = true,
            IsValid = true,
            CyclesConsumed = cyclesConsumed,
            AtPC = atPC,
        };
    }

    public static InstructionExecResult CpuAlreadyHaltedResult(ushort atPC)
    {
        return new InstructionExecResult(0)
        {
            UnknownInstruction = false,
            HaltedCpu = true,
            IsValid = false,
            CyclesConsumed = 0,
            AtPC = atPC,
        };
    }
}
