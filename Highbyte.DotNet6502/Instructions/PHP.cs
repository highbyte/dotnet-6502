using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Push Processor Status.
    /// Pushes a copy of the status flags on to the stack.
    /// </summary>
    public class PHP : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            // Set the Break flag on the copy of the ProcessorStatus that will be stored in stack.
            var processorStatusCopy = cpu.ProcessorStatus.Clone();
            processorStatusCopy.Break = true;
            processorStatusCopy.Unused = true;            
            cpu.PushByteToStack(processorStatusCopy.Value, mem);

            // Consume extra cycles to change SP?
            cpu.ExecState.CyclesConsumed++;
            return true;
        }
        
        public PHP()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.PHP,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 3,
                    }
            };
        }
    }
}
