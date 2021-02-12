using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Increment Memory.
    /// Adds one to the value held at a specified memory location setting the zero and negative flags as appropriate.
    /// </summary>
    public class INC : Instruction, IInstructionUsesByte
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        
        public InstructionLogicResult ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
        {
            value++;
            cpu.StoreByte(value, mem, addrModeCalcResult.InsAddress.Value);
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(value, cpu.ProcessorStatus);

            return InstructionLogicResult.WithNoExtraCycles();
        }

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            var insValue = cpu.FetchByte(mem, addrModeCalcResult.InsAddress.Value);
            
            if(addrModeCalcResult.OpCode.AddressingMode == AddrMode.ABS_X && !addrModeCalcResult.AddressCalculationCrossedPageBoundary)
            {
                // TODO: Does DEC_ABS_X (and not LDA_ABS_X) really always take an extra cycle even if final address didn't cross page boundary? 
                // Or wrong in documentation?
                cpu.ExecState.CyclesConsumed++;
            }

            insValue++;
            cpu.ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
            cpu.StoreByte(insValue, mem, addrModeCalcResult.InsAddress.Value);
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(insValue, cpu.ProcessorStatus);

            return true;
        }
        
        public INC()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.INC_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 5,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.INC_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.INC_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.INC_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        MinimumCycles = 7,
                    },
            };
        }
    }
}
