using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Logical Inclusive OR.
    /// An inclusive OR is performed, bit by bit, on the accumulator contents using the contents of a byte of memory.
    /// </summary>
    public class ORA : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            byte insValue = GetInstructionValueFromAddressOrDirectly(cpu, mem, addrModeCalcResult);

            cpu.A |= insValue;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, cpu.ProcessorStatus);
            return true;
            
        }

        public ORA()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {

                        Code = OpCodeId.ORA_I,
                        AddressingMode = AddrMode.I,
                        Size = 2,
                        MinimumCycles = 2,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ORA_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 3,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ORA_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ORA_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ORA_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        MinimumCycles = 4, // +1 if page boundary is crossed
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ORA_ABS_Y,
                        AddressingMode = AddrMode.ABS_Y,
                        Size = 3,
                        MinimumCycles = 4, // +1 if page boundary is crossed
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ORA_IX_IND,
                        AddressingMode = AddrMode.IX_IND,
                        Size = 2,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.ORA_IND_IX,
                        AddressingMode = AddrMode.IND_IX,
                        Size = 2,
                        MinimumCycles = 5, // +1 if page boundary is crossed
                    },
            };
        }
    }
}