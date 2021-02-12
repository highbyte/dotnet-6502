using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Pull Processor Status.
    /// Pulls an 8 bit value from the stack and into the processor flags. The flags will take on new states as determined by the value pulled.
    /// </summary>
    public class PLP : Instruction, IInstructionUsesStack
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        
        public InstructionLogicResult ExecuteWithStack(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.ProcessorStatus.Value = cpu.PopByteFromStack(mem);
            
            return InstructionLogicResult.WithNoExtraCycles();
        } 

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.ProcessorStatus.Value = cpu.PopByteFromStack(mem);
            // Consume two extra cycles to change SP? Why one more than PHP?
            cpu.ExecState.CyclesConsumed += 2;
            return true;
        }
        
        public PLP()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.PLP,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 4,
                    }
            };
        }
    }
}
