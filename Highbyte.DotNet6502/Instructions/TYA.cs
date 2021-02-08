using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Transfer Y to Accumulator.
    /// Copies the current contents of the Y register into the accumulator and sets the zero and negative flags as appropriate.
    /// </summary>
    public class TYA : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.A = cpu.Y;
            // Extra cycle for transfer register to another register?
            cpu.ExecState.CyclesConsumed++;                        
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, cpu.ProcessorStatus);

            return true;
        }
        
        public TYA()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = Ins.TYA,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        Cycles = 2,
                    }
            };
        }
    }
}
