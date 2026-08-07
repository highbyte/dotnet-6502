namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Page-crossing detection for relative branches, which decides whether a taken branch costs 3
/// cycles or 4.
///
/// Covered exhaustively in both directions: the rule is symmetric, but the implementation used to
/// treat forwards and backwards separately and only the forward half was ever asserted.
/// </summary>
public class BranchHelperTest
{
    [Theory]
    // Forwards, staying inside the page.
    [InlineData(0x2002, 0x20, 0x2022, false)]
    // Forwards, over the top of the page.
    [InlineData(0x20F2, 0x20, 0x2112, true)]
    // Backwards, staying inside the page — the loop case, and the one that used to be wrong.
    [InlineData(0x2052, -16, 0x2042, false)]
    [InlineData(0x10F2, -8, 0x10EA, false)]
    [InlineData(0x2002, -2, 0x2000, false)]
    // Backwards, under the bottom of the page.
    [InlineData(0x2007, -16, 0x1FF7, true)]
    [InlineData(0x2000, -1, 0x1FFF, true)]
    // The extremes of the signed offset range.
    [InlineData(0x2080, 127, 0x20FF, false)]
    [InlineData(0x2081, 127, 0x2100, true)]
    [InlineData(0x2080, -128, 0x2000, false)]
    [InlineData(0x207F, -128, 0x1FFF, true)]
    public void Page_Crossing_Is_Whether_The_Target_Lands_In_A_Different_Page(
        ushort pc, int offset, ushort expectedTarget, bool expectedCrossed)
    {
        var target = BranchHelper.CalculateNewAbsoluteBranchAddress(
            pc, (sbyte)offset, out var cyclesConsumed, out var crossed);

        Assert.Equal(expectedTarget, target);
        Assert.Equal(expectedCrossed, crossed);

        // Cross-check against the definition, so the table above cannot drift from the rule.
        Assert.Equal((pc & 0xFF00) != (target & 0xFF00), crossed);

        // A taken branch costs one cycle, and one more when it crosses.
        Assert.Equal(expectedCrossed ? 2UL : 1UL, cyclesConsumed);
    }

    /// <summary>
    /// -128 is the offset that cannot be negated inside a signed byte, and the reason the old
    /// implementation reached for a cast in the first place.
    /// </summary>
    [Fact]
    public void The_Most_Negative_Offset_Does_Not_Throw()
    {
        var target = BranchHelper.CalculateNewAbsoluteBranchAddress(0x2080, -128, out _, out var crossed);

        Assert.Equal(0x2000, target);
        Assert.False(crossed);
    }

    [Fact]
    public void Wrapping_Below_Zero_Stays_Within_The_Address_Space()
    {
        var target = BranchHelper.CalculateNewAbsoluteBranchAddress(0x0000, -1, out _, out var crossed);

        Assert.Equal(0xFFFF, target);
        Assert.True(crossed);
    }
}
