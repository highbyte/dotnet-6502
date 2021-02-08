using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Branch if Carry Set.
    /// If the carry flag is clear then add the relative displacement to the program counter to cause a branch to a new location.
    /// </summary>
    public class BCS : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            byte insValue = GetInstructionValueFromAddressOrDirectly(cpu, mem, addrModeCalcResult);

            if(cpu.ProcessorStatus.Carry)
            {
                // The instruction value is signed byte with the relative address (positive or negative)
                cpu.PC = BranchHelper.CalculateNewAbsoluteBranchAddress(cpu.PC, (sbyte)insValue, out ulong cyclesConsumed);
                cpu.ExecState.CyclesConsumed += cyclesConsumed;
            }
            return true;
        }

        public BCS()
        {
            _opCodes = new List<OpCode>
            {
                new OpCode
                {

                    Code = Ins.BCS,
                    AddressingMode = AddrMode.Relative,
                    Size = 2,
                    Cycles = 2, // +1 if branch succeeds +2 if to a new page
                },
            };
        }
    }
}
