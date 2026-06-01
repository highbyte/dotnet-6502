namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// SLO (illegal, also ASO) — ASL memory then ORA into A.
/// Shifts the value at the effective address one bit left (bit 0 = 0, old bit 7 → C),
/// then ORs the shifted result into A, setting N, Z, and C flags.
/// </summary>
public class SLO : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        value = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(value, ref cpu.ProcessorStatus);
        cpu.StoreByte(value, mem, addrModeCalcResult.InsAddress!.Value);
        cpu.A |= value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
        return 0;
    }

    public SLO()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.SLO_IX_IND, AddressingMode = AddrMode.IX_IND, Size = 2, MinimumCycles = 8 },
            new OpCode { Code = OpCodeId.SLO_ZP,     AddressingMode = AddrMode.ZP,     Size = 2, MinimumCycles = 5 },
            new OpCode { Code = OpCodeId.SLO_ABS,    AddressingMode = AddrMode.ABS,    Size = 3, MinimumCycles = 6 },
            new OpCode { Code = OpCodeId.SLO_IND_IX, AddressingMode = AddrMode.IND_IX, Size = 2, MinimumCycles = 8 },
            new OpCode { Code = OpCodeId.SLO_ZP_X,   AddressingMode = AddrMode.ZP_X,   Size = 2, MinimumCycles = 6 },
            new OpCode { Code = OpCodeId.SLO_ABS_Y,  AddressingMode = AddrMode.ABS_Y,  Size = 3, MinimumCycles = 7 },
            new OpCode { Code = OpCodeId.SLO_ABS_X,  AddressingMode = AddrMode.ABS_X,  Size = 3, MinimumCycles = 7 },
        };
    }
}
