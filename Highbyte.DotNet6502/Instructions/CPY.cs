using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Compare Y Register.
    /// This instruction compares the contents of the Y register with another memory held value and sets the zero and carry flags as appropriate.
    /// </summary>
    public class CPY : Instruction, IInstructionUsesByte
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public InstructionLogicResult ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
        {
            BinaryArithmeticHelpers.SetFlagsAfterCompare(cpu.Y, value, cpu.ProcessorStatus);

            return InstructionLogicResult.WithNoExtraCycles();
        }

        public CPY()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {

                        Code = OpCodeId.CPY_I,
                        AddressingMode = AddrMode.I,
                        Size = 2,
                        MinimumCycles = 2,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.CPY_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 3,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.CPY_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 4,
                    },
            };
        }
    }
}