using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Clear Overflow Flag.
    /// Clears the overflow flag.
    /// </summary>
    public class CLV : Instruction, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public ulong Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.ProcessorStatus.Overflow = false;
                        
            return 0;
        } 
        
        public CLV()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.CLV,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
