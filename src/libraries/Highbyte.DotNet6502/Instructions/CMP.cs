namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Compare.
/// This instruction compares the contents of the accumulator with another memory held value and sets the zero and carry flags as appropriate.
/// </summary>
public class CMP : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        BinaryArithmeticHelpers.SetFlagsAfterCompare(cpu.A, value, ref cpu.ProcessorStatus);

        return 
            InstructionExtraCyclesCalculator.CalculateExtraCycles(
                    addrModeCalcResult.OpCode.AddressingMode, 
                    addrModeCalcResult.AddressCalculationCrossedPageBoundary);
    }

    public CMP()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {

                    Code = OpCodeId.CMP_I,
                    AddressingMode = AddrMode.I,
                    Size = 2,
                    MinimumCycles = 2,
                },
                new OpCode
                {
                    Code = OpCodeId.CMP_ZP,
                    AddressingMode = AddrMode.ZP,
                    Size = 2,
                    MinimumCycles = 3,
                },
                new OpCode
                {
                    Code = OpCodeId.CMP_ZP_X,
                    AddressingMode = AddrMode.ZP_X,
                    Size = 2,
                    MinimumCycles = 4,
                },
                new OpCode
                {
                    Code = OpCodeId.CMP_ABS,
                    AddressingMode = AddrMode.ABS,
                    Size = 3,
                    MinimumCycles = 4,
                },
                new OpCode
                {
                    Code = OpCodeId.CMP_ABS_X,
                    AddressingMode = AddrMode.ABS_X,
                    Size = 3,
                    MinimumCycles = 4, // +1 if page boundary is crossed
                },
                new OpCode
                {
                    Code = OpCodeId.CMP_ABS_Y,
                    AddressingMode = AddrMode.ABS_Y,
                    Size = 3,
                    MinimumCycles = 4, // +1 if page boundary is crossed
                },
                new OpCode
                {
                    Code = OpCodeId.CMP_IX_IND,
                    AddressingMode = AddrMode.IX_IND,
                    Size = 2,
                    MinimumCycles = 6,
                },
                new OpCode
                {
                    Code = OpCodeId.CMP_IND_IX,
                    AddressingMode = AddrMode.IND_IX,
                    Size = 2,
                    MinimumCycles = 5, // +1 if page boundary is crossed
                },
        };
    }
}
