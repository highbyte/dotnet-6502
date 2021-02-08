using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Compare Y Register.
    /// This instruction compares the contents of the Y register with another memory held value and sets the zero and carry flags as appropriate.
    /// </summary>
    public class CPY : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            byte insValue = GetInstructionValueFromAddressOrDirectly(cpu, mem, addrModeCalcResult);
            BinaryArithmeticHelpers.SetFlagsAfterCompare(cpu.Y, insValue, cpu.ProcessorStatus);
            
            return true;
        }

        public CPY()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {

                        Code = Ins.CPY_I,
                        AddressingMode = AddrMode.I,
                        Size = 2,
                        Cycles = 2,
                    },
                    new OpCode
                    {
                        Code = Ins.CPY_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        Cycles = 3,
                    },
                    new OpCode
                    {
                        Code = Ins.CPY_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        Cycles = 4,
                    },
            };
        }
    }
}