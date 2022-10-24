using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Logical AND.
    /// A logical AND is performed, bit by bit, on the accumulator contents using the contents of a byte of memory.
    /// </summary>
    public class AND : Instruction, IInstructionUsesByte
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.A &= value;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, cpu.ProcessorStatus);

            return
                InstructionExtraCyclesCalculator.CalculateExtraCycles(
                        addrModeCalcResult.OpCode.AddressingMode,
                        addrModeCalcResult.AddressCalculationCrossedPageBoundary);
        }

        public AND()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {

                        Code = OpCodeId.AND_I,
                        AddressingMode = AddrMode.I,
                        Size = 2,
                        MinimumCycles = 2,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.AND_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 3,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.AND_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.AND_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.AND_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        MinimumCycles = 4, // +1 if page boundary is crossed
                    },
                    new OpCode
                    {
                        Code = OpCodeId.AND_ABS_Y,
                        AddressingMode = AddrMode.ABS_Y,
                        Size = 3,
                        MinimumCycles = 4, // +1 if page boundary is crossed
                    },
                    new OpCode
                    {
                        Code = OpCodeId.AND_IX_IND,
                        AddressingMode = AddrMode.IX_IND,
                        Size = 2,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.AND_IND_IX,
                        AddressingMode = AddrMode.IND_IX,
                        Size = 2,
                        MinimumCycles = 5, // +1 if page boundary is crossed
                    },
            };
        }
    }
}
