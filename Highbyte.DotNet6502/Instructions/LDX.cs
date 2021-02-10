using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Load X Register.
    /// Loads a byte of memory into the X register setting the zero and negative flags as appropriate.
    /// </summary>
    public class LDX : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            byte insValue = GetInstructionValueFromAddressOrDirectly(cpu, mem, addrModeCalcResult);

            cpu.X = insValue;
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, cpu.ProcessorStatus);

            return true;
        }

        public LDX()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.LDX_I,
                        AddressingMode = AddrMode.I,
                        Size = 2,
                        MinimumCycles = 2,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LDX_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 3,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LDX_ZP_Y,
                        AddressingMode = AddrMode.ZP_Y,
                        Size = 2,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LDX_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.LDX_ABS_Y,
                        AddressingMode = AddrMode.ABS_Y,
                        Size = 3,
                        MinimumCycles = 4, // +1 if page boundary is crossed
                    },
            };
        }
    }
}
