namespace Highbyte.DotNet6502;

public static class BranchHelper
{
    /// <summary>
    /// </summary>
    /// <param name="PC"></param>
    /// <param name="branchOffset"></param>
    /// <param name="cyclesConsumed"></param>
    /// <returns></returns>
    public static ushort CalculateNewAbsoluteBranchAddress(ushort PC, sbyte branchOffset, out ulong cyclesConsumed)
    {
        return CalculateNewAbsoluteBranchAddress(PC, branchOffset, out cyclesConsumed, out bool _);
    }

    /// <summary>
    /// </summary>
    /// <param name="PC"></param>
    /// <param name="branchOffset"></param>
    /// <param name="cyclesConsumed"></param>
    /// <returns></returns>
    public static ushort CalculateNewAbsoluteBranchAddress(ushort PC, sbyte branchOffset, out ulong cyclesConsumed, out bool addressCalculationCrossedPageBoundary)
    {
        cyclesConsumed=0;
        addressCalculationCrossedPageBoundary = false;
        // Check if adding offset to current address will cross page boundary. If so, one more cycle is consumed
        // TODO: Can smarter logic be written to handle positive vs negative branchOffset?
        if(branchOffset>=0)
        {
            if( (PC & 0x00ff) + branchOffset > 0xff)
            {
                cyclesConsumed++;
                addressCalculationCrossedPageBoundary  =true;
            }

        }
        else
        {
            // Abs(-128) = 128 which leads to Exception:
            //'System.OverflowException' occurred in System.Private.CoreLib.dll: 'Negating the minimum value of a twos complement number is invalid.'
            // Must cast to ushort first
            if( (PC & 0x00ff) < Math.Abs((ushort)branchOffset))
            {
                cyclesConsumed++;
                addressCalculationCrossedPageBoundary  =true;
            }
        }

        cyclesConsumed++;
        return (ushort)(PC + branchOffset);
    }


}
