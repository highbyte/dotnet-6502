using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Transfer X to Stack Pointer.
    /// Copies the current contents of the X register into the stack register.
    /// </summary>
    public class TXS : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.SP = cpu.X;
            // Extra cycle for transfer register to another register?
            cpu.ExecState.CyclesConsumed++;
            return true;
        }
        
        public TXS()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.TXS,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
