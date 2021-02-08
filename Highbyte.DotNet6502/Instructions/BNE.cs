using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Branch if Not Equal.
    /// If the zero flag is clear then add the relative displacement to the program counter to cause a branch to a new location.
    /// </summary>
    public class BNE : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            byte insValue = addrModeCalcResult.InsValue.Value;

            if(!cpu.ProcessorStatus.Zero)
            {
                // The instruction value is signed byte with the relative address (positive or negative)
                cpu.PC = BranchHelper.CalculateNewAbsoluteBranchAddress(cpu.PC, (sbyte)insValue, out ulong cyclesConsumed);
                cpu.ExecState.CyclesConsumed += cyclesConsumed;
            }
            return true;
        }

        public BNE()
        {
            _opCodes = new List<OpCode>
            {
                new OpCode
                {

                    Code = Ins.BNE,
                    AddressingMode = AddrMode.Relative,
                    Size = 1,
                    Cycles = 2, // +1 if branch succeeds +2 if to a new page
                },
            };
        }
    }
}
