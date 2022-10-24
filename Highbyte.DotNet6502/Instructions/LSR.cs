using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Logical Shift Right.
    /// Each of the bits in A or M is shift one place to the right. The bit that was in bit 0 is shifted into the carry flag. Bit 7 is set to zero.
    /// </summary>
    public class LSR : Instruction, IInstructionUsesAddress, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public ulong ExecuteWithWord(CPU cpu, Memory mem, ushort address, AddrModeCalcResult addrModeCalcResult)
        {
            var tempValue = cpu.FetchByte(mem, address);
            tempValue = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(tempValue, cpu.ProcessorStatus);
            cpu.StoreByte(tempValue, mem, address);               

            return InstructionLogicResult.WithNoExtraCycles();
        }        

        public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume Accumulator mode
            cpu.A = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(cpu.A, cpu.ProcessorStatus);

            return InstructionLogicResult.WithNoExtraCycles();
        }

        public LSR()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.LSR_ACC,
                        AddressingMode = AddrMode.Accumulator,
                        Size = 1,
                        MinimumCycles = 2,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LSR_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 5,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LSR_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LSR_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LSR_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        MinimumCycles = 7,
                    },
            };
        }
    }
}
