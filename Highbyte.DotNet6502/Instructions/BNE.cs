using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Branch if Not Equal.
    /// If the zero flag is clear then add the relative displacement to the program counter to cause a branch to a new location.
    /// </summary>
    public class BNE : Instruction, IInstructionUsesByte
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        
        public InstructionLogicResult ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
        {
            bool branchSucceeded = false;
            bool addressCalculationCrossedPageBoundary = false;
            if(!cpu.ProcessorStatus.Zero)
            {
                // The instruction value is signed byte with the relative address (positive or negative)
                cpu.PC = BranchHelper.CalculateNewAbsoluteBranchAddress(cpu.PC, (sbyte)value, out ulong _, out addressCalculationCrossedPageBoundary);
                branchSucceeded = true;
            }

            return InstructionLogicResult.WithExtraCycles(
                InstructionExtraCyclesCalculator.CalculateExtraCyclesForBranchInstructions(
                        branchSucceeded, 
                        addressCalculationCrossedPageBoundary)
                );
        }         

        public BNE()
        {
            _opCodes = new List<OpCode>
            {
                new OpCode
                {

                    Code = OpCodeId.BNE,
                    AddressingMode = AddrMode.Relative,
                    Size = 2,
                    MinimumCycles = 2, // +1 if branch succeeds +2 if to a new page
                },
            };
        }
    }
}
