using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Rotate Left.
    /// Move each of the bits in either A or M one place to the left.
    /// Bit 0 is filled with the current value of the carry flag whilst the old 
    /// bit 7 becomes the new carry flag value.
    /// </summary>
    public class ROL : Instruction, IInstructionUseAddress, IInstructionUseNone
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

                public InstructionLogicResult ExecuteWithWord(CPU cpu, Memory mem, ushort address, AddrModeCalcResult addrModeCalcResult)
        {
            var tempValue = cpu.FetchByte(mem, address);
            tempValue = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(tempValue, cpu.ProcessorStatus);
            cpu.StoreByte(tempValue, mem, address);

            return InstructionLogicResult.WithNoExtraCycles();
        }        

        public InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume Accumulator mode
            cpu.A = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(cpu.A, cpu.ProcessorStatus);

            return InstructionLogicResult.WithNoExtraCycles();
        }
        
        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            if(addrModeCalcResult.InsAddress.HasValue)
            {
                var insAddress = addrModeCalcResult.InsAddress.Value;
                var tempValue = cpu.FetchByte(mem, insAddress);
                tempValue = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(tempValue, cpu.ProcessorStatus);

                if(addrModeCalcResult.OpCode.AddressingMode == AddrMode.ABS_X)
                {
                    if(!addrModeCalcResult.AddressCalculationCrossedPageBoundary)
                        // TODO: Is this correCt: Two extra cycles for ASL before writing back to memory if we did NOT cross page boundary?
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
            cpu.A = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(cpu.A, cpu.ProcessorStatus);
            return true;
        }

        public ROL()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.ROL_ACC,
                        AddressingMode = AddrMode.Accumulator,
                        Size = 1,
                        MinimumCycles = 2,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ROL_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 5,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ROL_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ROL_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ROL_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        MinimumCycles = 7,
                    },
            };
        }
    }
}
