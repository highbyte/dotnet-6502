namespace Highbyte.DotNet6502.Instructions;

/// <summary>
/// Branch if Positive.
/// If the negative flag is clear then add the relative displacement to the program counter to cause a branch to a new location.
/// </summary>
public class BPL : Instruction, IInstructionUsesByte
{
    private readonly List<OpCode> _opCodes;
    public override List<OpCode> OpCodes => _opCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        bool branchSucceeded = false;
        bool addressCalculationCrossedPageBoundary = false;
        if(!cpu.ProcessorStatus.Negative)
        {
            // The instruction value is signed byte with the relative address (positive or negative)
            cpu.PC = BranchHelper.CalculateNewAbsoluteBranchAddress(cpu.PC, (sbyte)value, out ulong _, out addressCalculationCrossedPageBoundary);
            branchSucceeded = true;
        }

        return 
            InstructionExtraCyclesCalculator.CalculateExtraCyclesForBranchInstructions(
                    branchSucceeded, 
                    addressCalculationCrossedPageBoundary);
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
