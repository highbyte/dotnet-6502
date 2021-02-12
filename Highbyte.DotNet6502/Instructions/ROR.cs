using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Rotate Right.
    /// Move each of the bits in either A or M one place to the right.
    /// Bit 7 is filled with the current value of the carry flag whilst the 
    /// old bit 0 becomes the new carry flag value.
    /// </summary>
    public class ROR : Instruction, IInstructionUsesAddress, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

                public InstructionLogicResult ExecuteWithWord(CPU cpu, Memory mem, ushort address, AddrModeCalcResult addrModeCalcResult)
        {
            var tempValue = cpu.FetchByte(mem, address);
            tempValue = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(tempValue, cpu.ProcessorStatus);
            cpu.StoreByte(tempValue, mem, address);

            return InstructionLogicResult.WithNoExtraCycles();
        }        

        public InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume Accumulator mode
            cpu.A = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(cpu.A, cpu.ProcessorStatus);

            return InstructionLogicResult.WithNoExtraCycles();
        }
        
        public ROR()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.ROR_ACC,
                        AddressingMode = AddrMode.Accumulator,
                        Size = 1,
                        MinimumCycles = 2,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ROR_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 5,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ROR_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ROR_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ROR_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        MinimumCycles = 7,
                    },
            };
        }
    }
}
