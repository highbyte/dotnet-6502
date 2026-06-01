namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Halt the CPU until reset. Also known as KIL or HLT.
/// </summary>
public class JAM : Instruction, IInstructionUsesOnlyRegOrStatus
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
    {
        cpu.Halt();
        return 0;
    }

    public JAM()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.JAM_02, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_12, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_22, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_32, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_42, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_52, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_62, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_72, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_92, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_B2, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_D2, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
            new OpCode { Code = OpCodeId.JAM_F2, AddressingMode = AddrMode.Implied, Size = 1, MinimumCycles = 2 },
        };
    }
}
