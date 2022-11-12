using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Transfer X to Stack Pointer.
    /// Copies the current contents of the X register into the stack register.
    /// </summary>
    public class TXS : Instruction, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.SP = cpu.X;
            
            return 0;                
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
