using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Increment X Register.
    /// Adds one to the X register setting the zero and negative flags as appropriate.
    /// </summary>
    public class INX : Instruction, IInstructionUsesOnlyRegOrStatus
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public InstructionLogicResult Execute(CPU cpu, AddrModeCalcResult addrModeCalcResult)
        {
            // Assume implied mode
            cpu.X++;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, cpu.ProcessorStatus);

            return InstructionLogicResult.WithNoExtraCycles();
        }

        public INX()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.INX,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 2,
                    }
            };
        }
    }
}
