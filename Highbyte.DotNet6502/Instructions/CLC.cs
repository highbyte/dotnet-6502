using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Clear Carry Flag.
    /// Set the carry flag to zero.
    /// </summary>
    public class CLC : Instruction, IInstructionUseNone
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.ProcessorStatus.Carry = false;
            // Consume extra cycle to clear flag?
            cpu.ExecState.CyclesConsumed ++;
                        
            return InstructionLogicResult.WithNoExtraCycles();
        }

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.ProcessorStatus.Carry = false;
            // Consume extra cycle to clear flag?
            cpu.ExecState.CyclesConsumed ++;
            return true;
        }
        
        public CLC()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.CLC,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
