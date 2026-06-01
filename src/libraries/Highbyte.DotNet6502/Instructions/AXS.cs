namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// AXS (illegal, also SBX/SAX) — X = (A AND X) − immediate.
/// Computes the bitwise AND of A and X, subtracts the immediate byte without
/// borrow (carry is not used as input), stores the result in X, and sets
/// N, Z, and C flags as CMP does (C = (A&amp;X) >= imm).
/// </summary>
public class AXS : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        byte andVal = (byte)(cpu.A & cpu.X);
        BinaryArithmeticHelpers.SetFlagsAfterCompare(andVal, value, ref cpu.ProcessorStatus);
        cpu.X = (byte)(andVal - value);
        return 0;
    }

    public AXS()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.AXS_I, AddressingMode = AddrMode.I, Size = 2, MinimumCycles = 2 },
        };
    }
}
