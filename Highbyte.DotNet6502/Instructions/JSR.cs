using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Jump to Subroutine.
    /// The JSR instruction pushes the address (minus one) of the return point on to the stack and then sets the program counter to the target memory address.
    /// </summary>
    public class JSR : Instruction, IInstructionUsesAddress
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public ulong ExecuteWithWord(CPU cpu, Memory mem, ushort address, AddrModeCalcResult addrModeCalcResult)
        {
            // The JSR instruction pushes the address of the last byte of the instruction.
            // As PC now points to the next instruction, we push PC minus one to the stack.
            cpu.PushWordToStack((ushort) (cpu.PC-1), mem);
            // Set PC to address we will jump to
            cpu.PC = address;             

            return InstructionLogicResult.WithNoExtraCycles();
        }

        public JSR()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.JSR,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 6,
                    },
            };
        }
    }
}
