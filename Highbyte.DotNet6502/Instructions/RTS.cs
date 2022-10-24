using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Return from Subroutine.
    /// The RTS instruction is used at the end of a subroutine to return to the calling routine.
    /// It pulls the program counter (minus one) from the stack.
    /// </summary>
    public class RTS : Instruction, IInstructionUsesStack
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public ulong ExecuteWithStack(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            // Set PC back to the returning address from stack.
            // As the address that was pushed on stack by JSR was the last byte of the JSR instruction, 
            // we add one byte to the address we read from the stack (to get to next instruction)
            cpu.PC = (ushort) (cpu.PopWordFromStack(mem) + 1);

            return 0;
        }

        public RTS()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.RTS,
                        AddressingMode = AddrMode.Implied,
                        Size = 1,
                        MinimumCycles = 6,
                    }
            };
        }
    }
}
