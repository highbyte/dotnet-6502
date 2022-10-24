using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Jump.
    /// Sets the program counter to the address specified by the operand.
    /// </summary>
    public class JMP : Instruction, IInstructionUsesAddress
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public ulong ExecuteWithWord(CPU cpu, Memory mem, ushort address, AddrModeCalcResult addrModeCalcResult)
        {
           cpu.PC = address;

            return 0;
        }
        
        public JMP()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.JMP_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 3,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.JMP_IND,
                        AddressingMode = AddrMode.Indirect,
                        Size = 3,
                        MinimumCycles = 5,
                    },
            };
        }
    }
}
