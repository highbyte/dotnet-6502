using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Decrement Memory.
    /// Subtracts one from the value held at a specified memory location setting the zero and negative flags as appropriate.
    /// </summary>
    public class DEC : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            var insValue = cpu.FetchByte(mem, addrModeCalcResult.InsAddress.Value);
            
            if(addrModeCalcResult.OpCode.AddressingMode == AddrMode.ABS_X && !addrModeCalcResult.AddressCalculationCrossedPageBoundary)
            {
                // TODO: Does INC_ABS_X (and not LDA_ABS_X) really always take an extra cycle even if final address didn't cross page boundary? 
                // Or wrong in documentation?
                cpu.ExecState.CyclesConsumed++;
            }

            insValue--;
            cpu.ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
            cpu.StoreByte(insValue, mem, addrModeCalcResult.InsAddress.Value);
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(insValue, cpu.ProcessorStatus);

            return true;
        }
        
        public DEC()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = Ins.DEC_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        Cycles = 5,
                    },
                    new OpCode
                    {
                        Code = Ins.DEC_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        Cycles = 6,
                    },
                    new OpCode
                    {
                        Code = Ins.DEC_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        Cycles = 6,
                    },
                    new OpCode
                    {
                        Code = Ins.DEC_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        Cycles = 7,
                    },
            };
        }
    }
}
