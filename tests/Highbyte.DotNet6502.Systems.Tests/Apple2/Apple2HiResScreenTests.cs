using Highbyte.DotNet6502.Systems.Apple2.Video;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2HiResScreenTests
{
    [Theory]
    [InlineData(0, 0x0000)]
    [InlineData(1, 0x0400)]    // consecutive scan lines of a cell row sit $400 apart
    [InlineData(7, 0x1C00)]
    [InlineData(8, 0x0080)]    // next cell row within the band
    [InlineData(63, 0x1F80)]
    [InlineData(64, 0x0028)]   // second band
    [InlineData(128, 0x0050)]  // third band
    [InlineData(191, 0x1FD0)]  // last line
    public void Line_Offsets_Follow_The_HiRes_Interleave(int y, int expectedOffset)
    {
        Assert.Equal(expectedOffset, Apple2HiResScreen.GetLineOffset(y));
    }

    [Fact]
    public void Line_Addresses_Are_Relative_To_The_Page_Base()
    {
        Assert.Equal(0x2000, Apple2HiResScreen.GetLineStartAddress(0));
        Assert.Equal(0x2400, Apple2HiResScreen.GetLineStartAddress(1));
        Assert.Equal(0x4000, Apple2HiResScreen.GetLineStartAddress(0, Apple2HiResScreen.HiResPage2BaseAddress));
    }

    [Fact]
    public void All_Line_Offsets_Are_Distinct_And_Inside_The_Page()
    {
        var offsets = Enumerable.Range(0, Apple2HiResScreen.Lines)
            .Select(Apple2HiResScreen.GetLineOffset)
            .ToArray();

        Assert.Equal(offsets.Length, offsets.Distinct().Count());
        Assert.All(offsets, offset =>
            Assert.InRange(offset, 0, Apple2HiResScreen.HiResPageSize - Apple2HiResScreen.BytesPerLine));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(192)]
    public void An_Invalid_Line_Is_Rejected(int y)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Apple2HiResScreen.GetLineOffset(y));
    }
}
