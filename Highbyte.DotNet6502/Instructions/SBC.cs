using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Add with Carry
    /// </summary>
    public class SBC : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            byte insValue = GetInstructionValueFromAddressOrDirectly(cpu, mem, addrModeCalcResult);
                
            cpu.A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(cpu.A, insValue, cpu.ProcessorStatus);
            return true;
        }

        public SBC()
        {
            _opCodes = new List<OpCode>
            {
                new OpCode
                {

                    Code = Ins.SBC_I,
                    AddressingMode = AddrMode.I,
                    Size = 2,
                    Cycles = 2,
                },
                new OpCode
                {
                    Code = Ins.SBC_ZP,
                    AddressingMode = AddrMode.ZP,
                    Size = 2,
                    Cycles = 3,
                },
                new OpCode
                {
                    Code = Ins.SBC_ZP_X,
                    AddressingMode = AddrMode.ZP_X,
                    Size = 2,
                    Cycles = 4,
                },
                new OpCode
                {
                    Code = Ins.SBC_ABS,
                    AddressingMode = AddrMode.ABS,
                    Size = 3,
                    Cycles = 4,
                },
                new OpCode
                {
                    Code = Ins.SBC_ABS_X,
                    AddressingMode = AddrMode.ABS_X,
                    Size = 3,
                    Cycles = 4, // +1 if page boundary is crossed
                },
                new OpCode
                {
                    Code = Ins.SBC_ABS_Y,
                    AddressingMode = AddrMode.ABS_Y,
                    Size = 3,
                    Cycles = 4, // +1 if page boundary is crossed
                },
                new OpCode
                {
                    Code = Ins.SBC_IX_IND,
                    AddressingMode = AddrMode.IX_IND,
                    Size = 2,
                    Cycles = 6,
                },
                new OpCode
                {
                    Code = Ins.SBC_IND_IX,
                    AddressingMode = AddrMode.IND_IX,
                    Size = 2,
                    Cycles = 5, // +1 if page boundary is crossed
                },
            };

        }
    }
}
