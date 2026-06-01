namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// ISC (illegal, also ISB/INS) — Increment memory then SBC from A.
/// Increments the value at the effective address by 1, then subtracts the
/// result from A (with borrow), setting N, V, Z, and C flags as SBC does.
/// </summary>
public class ISC : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        value++;
        cpu.StoreByte(value, mem, addrModeCalcResult.InsAddress!.Value);
        cpu.A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(cpu.A, value, ref cpu.ProcessorStatus);
        return 0;
    }

    public ISC()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.ISC_IX_IND, AddressingMode = AddrMode.IX_IND, Size = 2, MinimumCycles = 8 },
            new OpCode { Code = OpCodeId.ISC_ZP,     AddressingMode = AddrMode.ZP,     Size = 2, MinimumCycles = 5 },
            new OpCode { Code = OpCodeId.ISC_ABS,    AddressingMode = AddrMode.ABS,    Size = 3, MinimumCycles = 6 },
            new OpCode { Code = OpCodeId.ISC_IND_IX, AddressingMode = AddrMode.IND_IX, Size = 2, MinimumCycles = 8 },
            new OpCode { Code = OpCodeId.ISC_ZP_X,   AddressingMode = AddrMode.ZP_X,   Size = 2, MinimumCycles = 6 },
            new OpCode { Code = OpCodeId.ISC_ABS_Y,  AddressingMode = AddrMode.ABS_Y,  Size = 3, MinimumCycles = 7 },
            new OpCode { Code = OpCodeId.ISC_ABS_X,  AddressingMode = AddrMode.ABS_X,  Size = 3, MinimumCycles = 7 },
        };
    }
}
