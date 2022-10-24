using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Compare X Register.
    /// This instruction compares the contents of the X register with another memory held value and sets the zero and carry flags as appropriate.
    /// </summary>
    public class CPX : Instruction, IInstructionUsesByte
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
        {
            BinaryArithmeticHelpers.SetFlagsAfterCompare(cpu.X, value, cpu.ProcessorStatus);

            return 0;
        }
        
        public CPX()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {

                        Code = OpCodeId.CPX_I,
                        AddressingMode = AddrMode.I,
                        Size = 2,
                        MinimumCycles = 2,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.CPX_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 3,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.CPX_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 4,
                    },
            };
        }
    }
}