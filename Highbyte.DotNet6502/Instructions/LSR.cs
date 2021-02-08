using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Logical Shift Right.
    /// Each of the bits in A or M is shift one place to the right. The bit that was in bit 0 is shifted into the carry flag. Bit 7 is set to zero.
    /// </summary>
    public class LSR : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            if(addrModeCalcResult.InsAddress.HasValue)
            {
                var insAddress = addrModeCalcResult.InsAddress.Value;
                var tempValue = cpu.FetchByte(mem, insAddress);
                tempValue = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(tempValue, cpu.ProcessorStatus);

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
            cpu.A = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(cpu.A, cpu.ProcessorStatus);
            return true;

        }

        public LSR()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = Ins.LSR_ACC,
                        AddressingMode = AddrMode.Accumulator,
                        Size = 1,
                        Cycles = 2,
                    },
                    new OpCode
                    {
                        Code = Ins.LSR_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        Cycles = 5,
                    },
                    new OpCode
                    {
                        Code = Ins.LSR_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        Cycles = 6,
                    },
                    new OpCode
                    {
                        Code = Ins.LSR_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        Cycles = 6,
                    },
                    new OpCode
                    {
                        Code = Ins.LSR_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        Cycles = 7,
                    },
            };
        }
    }
}
