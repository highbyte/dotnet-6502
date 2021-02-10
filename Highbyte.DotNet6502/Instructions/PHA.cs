using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Push Accumulator.
    /// Pushes a copy of the accumulator on to the stack.
    /// </summary>
    public class PHA : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.PushByteToStack(cpu.A, mem);
            // Consume extra cycles to change SP?
            cpu.ExecState.CyclesConsumed++;
            return true;
        }
        
        public PHA()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.PHA,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 3,
                    }
            };
        }
    }
}
