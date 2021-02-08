using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Decrement X Register.
    /// Subtracts one from the X register setting the zero and negative flags as appropriate.
    /// </summary>
    public class DEX : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.X--;
            cpu.ExecState.CyclesConsumed++;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, cpu.ProcessorStatus);
            return true;
        }
        
        public DEX()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = Ins.DEX,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        Cycles = 2,
                    }
            };
        }
    }
}
