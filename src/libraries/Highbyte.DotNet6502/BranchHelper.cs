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
        // PC is already past the instruction, which is what the offset is relative to. A taken
        // branch costs one cycle, plus one more when the target lands in a different page — and
        // "different page" is simply a different high byte, whichever direction the branch goes.
        //
        // This used to be computed from the offset in separate positive and negative cases. The
        // negative case read `Math.Abs((ushort)branchOffset)`, and casting a negative sbyte to
        // ushort gives its two's-complement value — 65528 for -8, not 8 — so the comparison was
        // always true and every backward branch was charged a page crossing it never made. Since
        // loops branch backwards, that was an extra cycle on essentially every loop iteration in
        // every emulated program.
        var target = (ushort)(PC + branchOffset);
        addressCalculationCrossedPageBoundary = (PC & 0xFF00) != (target & 0xFF00);
        cyclesConsumed = addressCalculationCrossedPageBoundary ? 2UL : 1UL;
        return target;
    }

}
