using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Load Y Register.
    /// Loads a byte of memory into the Y register setting the zero and negative flags as appropriate.
    /// </summary>
    public class LDY : Instruction, IInstructionUsesByte
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public InstructionLogicResult ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.Y = value;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.Y, cpu.ProcessorStatus);

            return InstructionLogicResult.WithExtraCycles(
                InstructionExtraCyclesCalculator.CalculateExtraCycles(
                        addrModeCalcResult.OpCode.AddressingMode, 
                        addrModeCalcResult.AddressCalculationCrossedPageBoundary)
                );
        } 

        public LDY()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.LDY_I,
                        AddressingMode = AddrMode.I,
                        Size = 2,
                        MinimumCycles = 2,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LDY_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 3,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LDY_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LDY_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LDY_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        MinimumCycles = 4, // +1 if page boundary is crossed
                    },
            };
        }
    }
}
