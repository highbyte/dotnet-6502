using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Transfer Stack Pointer to X.
    /// Copies the current contents of the stack register into the X register and sets the zero and negative flags as appropriate.
    /// </summary>
    public class TSX : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.X = cpu.SP;
            // Extra cycle for transfer register to another register?
            cpu.ExecState.CyclesConsumed++;                        
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, cpu.ProcessorStatus);
            return true;
        }
        
        public TSX()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = Ins.TSX,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        Cycles = 2,
                    }
            };
        }
    }
}
