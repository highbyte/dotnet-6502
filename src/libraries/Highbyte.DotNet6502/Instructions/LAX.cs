namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// LAX (illegal) — Load Accumulator and X Register.
/// Loads the same byte into both A and X; sets N and Z flags.
/// </summary>
public class LAX : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        cpu.A = value;
        cpu.X = value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(value, ref cpu.ProcessorStatus);

        return InstructionExtraCyclesCalculator.CalculateExtraCycles(
            addrModeCalcResult.OpCode.AddressingMode,
            addrModeCalcResult.AddressCalculationCrossedPageBoundary);
    }

    public LAX()
    {
        _opCodes = new List<OpCode>
        {
            new OpCode { Code = OpCodeId.LAX_IX_IND, AddressingMode = AddrMode.IX_IND, Size = 2, MinimumCycles = 6 },
            new OpCode { Code = OpCodeId.LAX_ZP,     AddressingMode = AddrMode.ZP,     Size = 2, MinimumCycles = 3 },
            new OpCode { Code = OpCodeId.LAX_ABS,    AddressingMode = AddrMode.ABS,    Size = 3, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.LAX_IND_IX, AddressingMode = AddrMode.IND_IX, Size = 2, MinimumCycles = 5 }, // +1 page cross
            new OpCode { Code = OpCodeId.LAX_ZP_Y,   AddressingMode = AddrMode.ZP_Y,   Size = 2, MinimumCycles = 4 },
            new OpCode { Code = OpCodeId.LAX_ABS_Y,  AddressingMode = AddrMode.ABS_Y,  Size = 3, MinimumCycles = 4 }, // +1 page cross
        };
    }
}
