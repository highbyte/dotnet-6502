using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Compare X Register.
    /// This instruction compares the contents of the X register with another memory held value and sets the zero and carry flags as appropriate.
    /// </summary>
    public class CPX : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            byte insValue = GetInstructionValueFromAddressOrDirectly(cpu, mem, addrModeCalcResult);
            BinaryArithmeticHelpers.SetFlagsAfterCompare(cpu.X, insValue, cpu.ProcessorStatus);
            
            return true;
        }

        public CPX()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {

                        Code = Ins.CPX_I,
                        AddressingMode = AddrMode.I,
                        Size = 2,
                        Cycles = 2,
                    },
                    new OpCode
                    {
                        Code = Ins.CPX_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        Cycles = 3,
                    },
                    new OpCode
                    {
                        Code = Ins.CPX_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        Cycles = 4,
                    },
            };
        }
    }
}