using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Clear Overflow Flag.
    /// Clears the overflow flag.
    /// </summary>
    public class CLV : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.ProcessorStatus.Overflow = false;
            // Consume extra cycle to clear flag?
            cpu.ExecState.CyclesConsumed ++;
            return true;
        }
        
        public CLV()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = Ins.CLV,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        Cycles = 2,
                    }
            };
        }
    }
}
