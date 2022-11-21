using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Load Accumulator.
/// Loads a byte of memory into the accumulator setting the zero and negative flags as appropriate.
/// </summary>
public class LDA : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;
    
    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        cpu.A = value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, cpu.ProcessorStatus);

        return 
            InstructionExtraCyclesCalculator.CalculateExtraCycles(
                    addrModeCalcResult.OpCode.AddressingMode, 
                    addrModeCalcResult.AddressCalculationCrossedPageBoundary);
    }

    public LDA()
    {
        _opCodes = new List<OpCode>
            {
                new OpCode
                {

                    Code = OpCodeId.LDA_I,
                    AddressingMode = AddrMode.I,
                    Size = 2,
                    MinimumCycles = 2,
                },
                new OpCode
                {
                    Code = OpCodeId.LDA_ZP,
                    AddressingMode = AddrMode.ZP,
                    Size = 2,
                    MinimumCycles = 3,
                },
                new OpCode
                {
                    Code = OpCodeId.LDA_ZP_X,
                    AddressingMode = AddrMode.ZP_X,
                    Size = 2,
                    MinimumCycles = 4,
                },
                new OpCode
                {
                    Code = OpCodeId.LDA_ABS,
                    AddressingMode = AddrMode.ABS,
                    Size = 3,
                    MinimumCycles = 4,
                },
                new OpCode
                {
                    Code = OpCodeId.LDA_ABS_X,
                    AddressingMode = AddrMode.ABS_X,
                    Size = 3,
                    MinimumCycles = 4, // +1 if page boundary is crossed
                },
                new OpCode
                {
                    Code = OpCodeId.LDA_ABS_Y,
                    AddressingMode = AddrMode.ABS_Y,
                    Size = 3,
                    MinimumCycles = 4, // +1 if page boundary is crossed
                },
                new OpCode
                {
                    Code = OpCodeId.LDA_IX_IND,
                    AddressingMode = AddrMode.IX_IND,
                    Size = 2,
                    MinimumCycles = 6,
                },
                new OpCode
                {
                    Code = OpCodeId.LDA_IND_IX,
                    AddressingMode = AddrMode.IND_IX,
                    Size = 2,
                    MinimumCycles = 5, // +1 if page boundary is crossed
                },
        };
    }
}
