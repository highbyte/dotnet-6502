using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Decrement X Register.
    /// Subtracts one from the X register setting the zero and negative flags as appropriate.
    /// </summary>
    public class DEX : Instruction, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.X--;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, cpu.ProcessorStatus);

            return InstructionLogicResult.WithNoExtraCycles();
        }

        public DEX()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.DEX,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
