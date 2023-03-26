namespace Highbyte.DotNet6502;

/// <summary>
/// Calculates extra clock cycles for instructions in certain addressing modes and circumstances.
/// </summary>
public class InstructionExtraCyclesCalculator
{
    public InstructionExtraCyclesCalculator()
    {
    }

    //  TODO: Don't use static method, create en interface and inject in consumer.
    public static ulong CalculateExtraCycles(AddrMode addrMode, bool addressCalculationCrossedPageBoundary)
    {
        ulong extraCycles = 0;
        if(addressCalculationCrossedPageBoundary
                && (addrMode == AddrMode.ABS_X 
                    || addrMode == AddrMode.ABS_Y
                    || addrMode == AddrMode.IND_IX
                     ))
            extraCycles ++;
        return extraCycles;
    }

    public static ulong CalculateExtraCyclesForBranchInstructions(bool branchSucceeded, bool addressCalculationCrossedPageBoundary)
    {
        if(!branchSucceeded)
            return 0;

        ulong extraCycles = 0;
        extraCycles++;
        
        if(addressCalculationCrossedPageBoundary)
            extraCycles += 1;
        return extraCycles;
    }
}