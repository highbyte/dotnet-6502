using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Compare.
    /// This instruction compares the contents of the accumulator with another memory held value and sets the zero and carry flags as appropriate.
    /// </summary>
    public class CMP : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            byte insValue = GetInstructionValueFromAddressOrDirectly(cpu, mem, addrModeCalcResult);
            BinaryArithmeticHelpers.SetFlagsAfterCompare(cpu.A, insValue, cpu.ProcessorStatus);
            
            return true;
        }

        public CMP()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {

                        Code = Ins.CMP_I,
                        AddressingMode = AddrMode.I,
                        Size = 2,
                        Cycles = 2,
                    },
                    new OpCode
                    {
                        Code = Ins.CMP_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        Cycles = 3,
                    },
                    new OpCode
                    {
                        Code = Ins.CMP_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        Cycles = 4,
                    },
                    new OpCode
                    {
                        Code = Ins.CMP_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        Cycles = 4,
                    },
                    new OpCode
                    {
                        Code = Ins.CMP_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        Cycles = 4, // +1 if page boundary is crossed
                    },
                    new OpCode
                    {
                        Code = Ins.CMP_ABS_Y,
                        AddressingMode = AddrMode.ABS_Y,
                        Size = 3,
                        Cycles = 4, // +1 if page boundary is crossed
                    },
                    new OpCode
                    {
                        Code = Ins.CMP_IX_IND,
                        AddressingMode = AddrMode.IX_IND,
                        Size = 2,
                        Cycles = 6,
                    },
                    new OpCode
                    {
                        Code = Ins.CMP_IND_IX,
                        AddressingMode = AddrMode.IND_IX,
                        Size = 2,
                        Cycles = 5, // +1 if page boundary is crossed
                    },
            };
        }
    }
}