namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// RRA (illegal) — ROR memory then ADC into A.
/// Rotates the value at the effective address one bit right through carry,
/// then adds the rotated result to A with carry, setting N, V, Z, and C flags.
/// </summary>
public class RRA : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        value = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(value, ref cpu.ProcessorStatus);
        cpu.StoreByte(value, mem, addrModeCalcResult.InsAddress!.Value);
        cpu.A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(cpu.A, value, ref cpu.ProcessorStatus);
        return 0;
    }

    public RRA()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.RRA_IX_IND, AddressingMode = AddrMode.IX_IND, Size = 2, MinimumCycles = 8 },
            new OpCode { Code = OpCodeId.RRA_ZP,     AddressingMode = AddrMode.ZP,     Size = 2, MinimumCycles = 5 },
            new OpCode { Code = OpCodeId.RRA_ABS,    AddressingMode = AddrMode.ABS,    Size = 3, MinimumCycles = 6 },
            new OpCode { Code = OpCodeId.RRA_IND_IX, AddressingMode = AddrMode.IND_IX, Size = 2, MinimumCycles = 8 },
            new OpCode { Code = OpCodeId.RRA_ZP_X,   AddressingMode = AddrMode.ZP_X,   Size = 2, MinimumCycles = 6 },
            new OpCode { Code = OpCodeId.RRA_ABS_Y,  AddressingMode = AddrMode.ABS_Y,  Size = 3, MinimumCycles = 7 },
            new OpCode { Code = OpCodeId.RRA_ABS_X,  AddressingMode = AddrMode.ABS_X,  Size = 3, MinimumCycles = 7 },
        };
    }
}
