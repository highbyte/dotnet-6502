using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Arithmetic Shift Left.
    /// This operation shifts all the bits of the accumulator or memory contents one bit left.
    /// Bit 0 is set to 0 and bit 7 is placed in the carry flag. The effect of this operation is to 
    /// multiply the memory contents by 2 (ignoring 2's complement considerations), setting the carry 
    /// if the result will not fit in 8 bits.
    /// </summary>
    public class ASL : Instruction, IInstructionUsesAddress, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public ulong ExecuteWithWord(CPU cpu, Memory mem, ushort address, AddrModeCalcResult addrModeCalcResult)
        {
            var tempValue = cpu.FetchByte(mem, address);
            tempValue = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(tempValue, cpu.ProcessorStatus);
            cpu.StoreByte(tempValue, mem, address);

            return 0;
        }

        public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume Accumulator mode
            cpu.A = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(cpu.A, cpu.ProcessorStatus);

            return 0;
        }

        public ASL()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.ASL_ACC,
                        AddressingMode = AddrMode.Accumulator,
                        Size = 1,
                        MinimumCycles = 2,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ASL_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 5,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ASL_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ASL_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ASL_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        MinimumCycles = 7,
                    },
            };
        }
    }
}
