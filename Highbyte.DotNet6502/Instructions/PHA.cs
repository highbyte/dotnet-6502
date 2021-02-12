using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Push Accumulator.
    /// Pushes a copy of the accumulator on to the stack.
    /// </summary>
    public class PHA : Instruction, IInstructionUsesStack
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        
        public InstructionLogicResult ExecuteWithStack(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.PushByteToStack(cpu.A, mem);
            return InstructionLogicResult.WithNoExtraCycles();
        }        

        public PHA()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.PHA,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 3,
                    }
            };
        }
    }
}
