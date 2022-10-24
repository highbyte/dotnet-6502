using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Return from Interrupt.
    /// The RTI instruction is used at the end of an interrupt processing routine.
    /// It pulls the processor flags from the stack followed by the program counter.
    /// </summary>
    public class RTI : Instruction, IInstructionUsesStack
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public ulong ExecuteWithStack(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.ProcessorStatus.Value = cpu.PopByteFromStack(mem);
            cpu.ProcessorStatus.Break = false;
            cpu.ProcessorStatus.Unused = false;
            cpu.PC = cpu.PopWordFromStack(mem);

            return InstructionLogicResult.WithNoExtraCycles();
        }

        
        public RTI()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.RTI,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 6,
                    }
            };
        }
    }
}
