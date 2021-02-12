using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Add with Carry
    /// </summary>
    public class ADC : Instruction, IInstructionUsesByte
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        
        public InstructionLogicResult ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(cpu.A, value, cpu.ProcessorStatus);

            return InstructionLogicResult.WithExtraCycles(
                InstructionExtraCyclesCalculator.CalculateExtraCycles(
                        addrModeCalcResult.OpCode.AddressingMode, 
                        addrModeCalcResult.AddressCalculationCrossedPageBoundary)
                );
        }

        public ADC()
        {
            _opCodes = new List<OpCode>
            {
                new OpCode
                {

                    Code = OpCodeId.ADC_I,
                    AddressingMode = AddrMode.I,
                    Size = 2,
                    MinimumCycles = 2,
                },
                new OpCode
                {
                    Code = OpCodeId.ADC_ZP,
                    AddressingMode = AddrMode.ZP,
                    Size = 2,
                    MinimumCycles = 3,
                },
                new OpCode
                {
                    Code = OpCodeId.ADC_ZP_X,
                    AddressingMode = AddrMode.ZP_X,
                    Size = 2,
                    MinimumCycles = 4,
                },
                new OpCode
                {
                    Code = OpCodeId.ADC_ABS,
                    AddressingMode = AddrMode.ABS,
                    Size = 3,
                    MinimumCycles = 4,
                },
                new OpCode
                {
                    Code = OpCodeId.ADC_ABS_X,
                    AddressingMode = AddrMode.ABS_X,
                    Size = 3,
                    MinimumCycles = 4, // +1 if page boundary is crossed
                },
                new OpCode
                {
                    Code = OpCodeId.ADC_ABS_Y,
                    AddressingMode = AddrMode.ABS_Y,
                    Size = 3,
                    MinimumCycles = 4, // +1 if page boundary is crossed
                },
                new OpCode
                {
                    Code = OpCodeId.ADC_IX_IND,
                    AddressingMode = AddrMode.IX_IND,
                    Size = 2,
                    MinimumCycles = 6,
                },
                new OpCode
                {
                    Code = OpCodeId.ADC_IND_IX,
                    AddressingMode = AddrMode.IND_IX,
                    Size = 2,
                    MinimumCycles = 5, // +1 if page boundary is crossed
                },
            };

        }
    }
}
