using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Increment Y Register.
    /// Adds one to the Y register setting the zero and negative flags as appropriate.
    /// </summary>
    public class INY : Instruction, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.Y++;
            cpu.ExecState.CyclesConsumed++;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.Y, cpu.ProcessorStatus);

            return InstructionLogicResult.WithNoExtraCycles();
        }
        
        public INY()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.INY,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
