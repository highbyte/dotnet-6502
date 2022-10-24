using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Decrement Memory.
    /// Subtracts one from the value held at a specified memory location setting the zero and negative flags as appropriate.
    /// </summary>
    public class DEC : Instruction, IInstructionUsesByte
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
        {
            value--;
            cpu.StoreByte(value, mem, addrModeCalcResult.InsAddress.Value);
            BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(value, cpu.ProcessorStatus);

            return 0;
        }

        public DEC()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.DEC_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 5,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.DEC_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.DEC_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.DEC_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        MinimumCycles = 7,
                    },
            };
        }
    }
}
