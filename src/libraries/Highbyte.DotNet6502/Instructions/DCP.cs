namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// DCP (illegal) — Decrement memory then CMP with A.
/// Decrements the value at the effective address by 1, then compares the result
/// with the accumulator setting N, Z, and C flags as CMP does.
/// </summary>
public class DCP : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        value--;
        cpu.StoreByte(value, mem, addrModeCalcResult.InsAddress!.Value);
        BinaryArithmeticHelpers.SetFlagsAfterCompare(cpu.A, value, ref cpu.ProcessorStatus);
        return 0;
    }

    public DCP()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.DCP_IX_IND, AddressingMode = AddrMode.IX_IND, Size = 2, MinimumCycles = 8 },
            new OpCode { Code = OpCodeId.DCP_ZP,     AddressingMode = AddrMode.ZP,     Size = 2, MinimumCycles = 5 },
            new OpCode { Code = OpCodeId.DCP_ABS,    AddressingMode = AddrMode.ABS,    Size = 3, MinimumCycles = 6 },
            new OpCode { Code = OpCodeId.DCP_IND_IX, AddressingMode = AddrMode.IND_IX, Size = 2, MinimumCycles = 8 },
            new OpCode { Code = OpCodeId.DCP_ZP_X,   AddressingMode = AddrMode.ZP_X,   Size = 2, MinimumCycles = 6 },
            new OpCode { Code = OpCodeId.DCP_ABS_Y,  AddressingMode = AddrMode.ABS_Y,  Size = 3, MinimumCycles = 7 },
            new OpCode { Code = OpCodeId.DCP_ABS_X,  AddressingMode = AddrMode.ABS_X,  Size = 3, MinimumCycles = 7 },
        };
    }
}
