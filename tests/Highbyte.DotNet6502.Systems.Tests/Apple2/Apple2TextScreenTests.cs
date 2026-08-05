using Highbyte.DotNet6502.Systems.Apple2.Video;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2TextScreenTests
{
    /// <summary>
    /// The canonical row-base table for text page 1. Hard-coded here on purpose: it is the
    /// documented hardware layout, independent of the formula the implementation uses.
    /// </summary>
    public static TheoryData<int, ushort> RowStartAddresses => new()
    {
        { 0, 0x0400 }, { 1, 0x0480 }, { 2, 0x0500 }, { 3, 0x0580 },
        { 4, 0x0600 }, { 5, 0x0680 }, { 6, 0x0700 }, { 7, 0x0780 },
        { 8, 0x0428 }, { 9, 0x04A8 }, { 10, 0x0528 }, { 11, 0x05A8 },
        { 12, 0x0628 }, { 13, 0x06A8 }, { 14, 0x0728 }, { 15, 0x07A8 },
        { 16, 0x0450 }, { 17, 0x04D0 }, { 18, 0x0550 }, { 19, 0x05D0 },
        { 20, 0x0650 }, { 21, 0x06D0 }, { 22, 0x0750 }, { 23, 0x07D0 },
    };

    [Theory]
    [MemberData(nameof(RowStartAddresses))]
    public void GetRowStartAddress_Matches_Hardware_Interleave_Table(int row, ushort expectedAddress)
    {
        Assert.Equal(expectedAddress, Apple2TextScreen.GetRowStartAddress(row));
    }

    [Fact]
    public void GetRowStartAddress_Uses_Page2_Base_When_Requested()
    {
        Assert.Equal((ushort)0x0800, Apple2TextScreen.GetRowStartAddress(0, Apple2TextScreen.TextPage2BaseAddress));
        Assert.Equal((ushort)0x0BD0, Apple2TextScreen.GetRowStartAddress(23, Apple2TextScreen.TextPage2BaseAddress));
    }

    [Fact]
    public void GetAddress_Adds_Column_To_Row_Start()
    {
        Assert.Equal((ushort)0x0428, Apple2TextScreen.GetAddress(8, 0));
        Assert.Equal((ushort)0x044F, Apple2TextScreen.GetAddress(8, 39));
    }

    [Fact]
    public void Every_Character_Cell_Maps_To_A_Distinct_Address()
    {
        var seen = new HashSet<ushort>();
        for (var row = 0; row < Apple2TextScreen.Rows; row++)
        {
            for (var col = 0; col < Apple2TextScreen.Columns; col++)
                Assert.True(seen.Add(Apple2TextScreen.GetAddress(row, col)), $"Duplicate address for row {row}, col {col}.");
        }

        Assert.Equal(Apple2TextScreen.Rows * Apple2TextScreen.Columns, seen.Count);
    }

    [Fact]
    public void TryGetRowColumn_Is_The_Inverse_Of_GetAddress()
    {
        for (var row = 0; row < Apple2TextScreen.Rows; row++)
        {
            for (var col = 0; col < Apple2TextScreen.Columns; col++)
            {
                var address = Apple2TextScreen.GetAddress(row, col);
                Assert.True(Apple2TextScreen.TryGetRowColumn(address, out var resolvedRow, out var resolvedCol));
                Assert.Equal(row, resolvedRow);
                Assert.Equal(col, resolvedCol);
            }
        }
    }

    [Fact]
    public void Screen_Holes_Are_Not_Part_Of_The_Displayed_Area()
    {
        // Each 128-byte block ends with 8 bytes the display never reads: $x78-$x7F.
        var holeCount = 0;
        for (var address = Apple2TextScreen.TextPage1BaseAddress;
             address < Apple2TextScreen.TextPage1BaseAddress + Apple2TextScreen.TextPageSize;
             address++)
        {
            var isHole = Apple2TextScreen.IsScreenHole((ushort)address);
            Assert.Equal(isHole, !Apple2TextScreen.TryGetRowColumn((ushort)address, out _, out _));
            if (isHole)
                holeCount++;
        }

        Assert.Equal(8 * 8, holeCount);
    }

    [Fact]
    public void IsScreenHole_Is_False_Outside_The_Text_Page()
    {
        Assert.False(Apple2TextScreen.IsScreenHole(0x03FF));
        Assert.False(Apple2TextScreen.IsScreenHole(0x0800));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void GetRowStartAddress_Rejects_Rows_Outside_The_Screen(int row)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Apple2TextScreen.GetRowStartAddress(row));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(40)]
    public void GetAddress_Rejects_Columns_Outside_The_Screen(int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Apple2TextScreen.GetAddress(0, column));
    }
}
