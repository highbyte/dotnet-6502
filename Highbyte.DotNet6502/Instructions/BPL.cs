using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Branch if Positive.
    /// If the negative flag is clear then add the relative displacement to the program counter to cause a branch to a new location.
    /// </summary>
    public class BPL : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            byte insValue = addrModeCalcResult.InsValue.Value;

            if(!cpu.ProcessorStatus.Negative)
            {
                // The instruction value is signed byte with the relative address (positive or negative)
                cpu.PC = BranchHelper.CalculateNewAbsoluteBranchAddress(cpu.PC, (sbyte)insValue, out ulong cyclesConsumed);
                cpu.ExecState.CyclesConsumed += cyclesConsumed;
            }
            return true;
        }

        public BPL()
        {
            _opCodes = new List<OpCode>
            {
                new OpCode
                {

                    Code = OpCodeId.BPL,
                    AddressingMode = AddrMode.Relative,
                    Size = 2,
                    MinimumCycles = 2, // +1 if branch succeeds +2 if to a new page
                },
            };
        }
    }
}
