using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Transfer Accumulator to Y.
    /// Copies the current contents of the accumulator into the Y register and sets the zero and negative flags as appropriate.
    /// </summary>
    public class TAY : Instruction, IInstructionUseNone
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.Y = cpu.A;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.Y, cpu.ProcessorStatus);
            
            return InstructionLogicResult.WithNoExtraCycles();                
        }

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.Y = cpu.A;
            // Extra cycle for transfer register to another register?
            cpu.ExecState.CyclesConsumed++;                        
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.Y, cpu.ProcessorStatus);

            return true;
        }
        
        public TAY()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.TAY,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
