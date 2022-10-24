using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Store Accumulator.
    /// Stores the contents of the accumulator into memory.
    /// </summary>
    public class STA : Instruction, IInstructionUsesAddress
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;
        public ulong ExecuteWithWord(CPU cpu, Memory mem, ushort address, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.StoreByte(cpu.A, mem, address);

            return InstructionLogicResult.WithNoExtraCycles();          
        }

        public STA()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.STA_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 3,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.STA_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.STA_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.STA_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        MinimumCycles = 5,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.STA_ABS_Y,
                        AddressingMode = AddrMode.ABS_Y,
                        Size = 3,
                        MinimumCycles = 5,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.STA_IX_IND,
                        AddressingMode = AddrMode.IX_IND,
                        Size = 2,
                        MinimumCycles = 6,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.STA_IND_IX,
                        AddressingMode = AddrMode.IND_IX,
                        Size = 2,
                        MinimumCycles = 6,
                    },
            };
        }
    }
}