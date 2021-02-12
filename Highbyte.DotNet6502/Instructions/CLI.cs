using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Clear Interrupt Disable.
    /// Clears the interrupt disable flag allowing normal interrupt requests to be serviced.
    /// </summary>
    public class CLI : Instruction, IInstructionUseNone
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.ProcessorStatus.InterruptDisable = false;
            // Consume extra cycle to clear flag?
            cpu.ExecState.CyclesConsumed ++;
                        
            return InstructionLogicResult.WithNoExtraCycles();
        }        

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.ProcessorStatus.InterruptDisable = false;
            // Consume extra cycle to clear flag?
            cpu.ExecState.CyclesConsumed ++;
            return true;
        }
        
        public CLI()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.CLI,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
