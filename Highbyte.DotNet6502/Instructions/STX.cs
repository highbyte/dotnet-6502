using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Store X Register.
    /// Stores the contents of the X register into memory.
    /// </summary>
    public class STX : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.StoreByte(cpu.X, mem, addrModeCalcResult.InsAddress.Value);            
            return true;
        }

        public STX()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = OpCodeId.STX_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        MinimumCycles = 3,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.STX_ZP_Y,
                        AddressingMode = AddrMode.ZP_Y,
                        Size = 2,
                        MinimumCycles = 4,
                    },
                    new OpCode
                    {
                        Code = OpCodeId.STX_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        MinimumCycles = 4,
                    },
            };
        }
    }
}
