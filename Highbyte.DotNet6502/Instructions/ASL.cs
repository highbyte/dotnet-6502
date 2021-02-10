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
    public class ASL : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            if(addrModeCalcResult.InsAddress.HasValue)
            {
                var insAddress = addrModeCalcResult.InsAddress.Value;
                var tempValue = cpu.FetchByte(mem, insAddress);
                tempValue = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(tempValue, cpu.ProcessorStatus);

                if(addrModeCalcResult.OpCode.AddressingMode == AddrMode.ABS_X)
                {
                    if(!addrModeCalcResult.AddressCalculationCrossedPageBoundary)
                        // TODO: Is this correct: Two extra cycles for ASL before writing back to memory if we did NOT cross page boundary?
                        cpu.ExecState.CyclesConsumed += 2;
                    else
                        // TODO: Is this correct: Extra cycle if the address + X crosses page boundary (1 extra was already added in CalcFullAddressX)
                        cpu.ExecState.CyclesConsumed ++;
                }
                else
                {
                    // Extra cycle for ASL? before writing back to memory?
                    cpu.ExecState.CyclesConsumed++;
                }

                cpu.StoreByte(tempValue, mem, insAddress);            
                return true;
            }

            // Assume Accumulator mode
            cpu.A = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(cpu.A, cpu.ProcessorStatus);
            return true;
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
