using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Clear Decimal Mode.
    /// Sets the decimal mode flag to zero.
    /// </summary>
    public class CLD : Instruction, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        
        public InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.ProcessorStatus.Decimal = false;
                        
            return InstructionLogicResult.WithNoExtraCycles();
        }

        public CLD()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.CLD,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
