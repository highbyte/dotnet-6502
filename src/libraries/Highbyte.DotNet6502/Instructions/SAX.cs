namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// SAX (illegal) — Store A AND X.
/// Stores the bitwise AND of A and X to memory. Flags are not affected.
/// </summary>
public class SAX : Instruction, IInstructionUsesAddress
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithWord(CPU cpu, Memory mem, ushort address, AddrModeCalcResult addrModeCalcResult)
    {
        cpu.StoreByte((byte)(cpu.A & cpu.X), mem, address);
        return 0;
    }

    public SAX()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.SAX_IX_IND, AddressingMode = AddrMode.IX_IND, Size = 2, MinimumCycles = 6 },
            new OpCode { Code = OpCodeId.SAX_ZP,     AddressingMode = AddrMode.ZP,     Size = 2, MinimumCycles = 3 },
            new OpCode { Code = OpCodeId.SAX_ABS,    AddressingMode = AddrMode.ABS,    Size = 3, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.SAX_ZP_Y,   AddressingMode = AddrMode.ZP_Y,   Size = 2, MinimumCycles = 4 },
        };
    }
}
