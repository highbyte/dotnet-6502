namespace Highbyte.DotNet6502.Systems.Apple2.Video;

/// <summary>
/// Apple II text-page addressing.
///
/// Text rows are <b>not</b> laid out linearly in memory. A row's offset from the page base is
/// <c>(row % 8) * $80 + (row / 8) * $28</c>, which groups the 24 rows into three interleaved
/// bands of eight. Each 128-byte block therefore holds three non-adjacent 40-byte rows plus
/// 8 unused "screen hole" bytes ($78-$7F of the block) that the firmware uses as scratch space
/// and the display never reads.
/// </summary>
public static class Apple2TextScreen
{
    public const int Columns = 40;
    public const int Rows = 24;

    /// <summary>Text page 1 base address ($0400-$07FF).</summary>
    public const ushort TextPage1BaseAddress = 0x0400;

    /// <summary>Text page 2 base address ($0800-$0BFF).</summary>
    public const ushort TextPage2BaseAddress = 0x0800;

    /// <summary>Size of a text page, including the unused screen-hole bytes.</summary>
    public const int TextPageSize = 0x0400;

    private static readonly ushort[] s_rowOffsets = BuildRowOffsets();

    private static ushort[] BuildRowOffsets()
    {
        var offsets = new ushort[Rows];
        for (var row = 0; row < Rows; row++)
            offsets[row] = (ushort)(((row % 8) * 0x80) + ((row / 8) * 0x28));
        return offsets;
    }

    /// <summary>Offset of a text row from the start of its text page.</summary>
    public static ushort GetRowOffset(int row)
    {
        ValidateRow(row);
        return s_rowOffsets[row];
    }

    /// <summary>Address of the first character cell of a text row.</summary>
    public static ushort GetRowStartAddress(int row, ushort pageBaseAddress = TextPage1BaseAddress)
        => (ushort)(pageBaseAddress + GetRowOffset(row));

    /// <summary>Address of a single character cell.</summary>
    public static ushort GetAddress(int row, int column, ushort pageBaseAddress = TextPage1BaseAddress)
    {
        ValidateColumn(column);
        return (ushort)(GetRowStartAddress(row, pageBaseAddress) + column);
    }

    /// <summary>
    /// Reverse mapping: resolves an address inside a text page to its row/column, or returns
    /// false when the address falls in one of the 8-byte screen holes (or outside the page).
    /// </summary>
    public static bool TryGetRowColumn(ushort address, out int row, out int column, ushort pageBaseAddress = TextPage1BaseAddress)
    {
        row = -1;
        column = -1;

        if (address < pageBaseAddress || address >= pageBaseAddress + TextPageSize)
            return false;

        var offset = address - pageBaseAddress;
        var block = offset / 0x80;          // 0-7, selects the row within each band
        var withinBlock = offset % 0x80;    // 0-127
        var band = withinBlock / 0x28;      // 0-2, selects the band (rows 0-7, 8-15, 16-23)

        if (band > 2)
            return false;                   // screen hole: $78-$7F of the block

        row = (band * 8) + block;
        column = withinBlock % 0x28;
        return true;
    }

    /// <summary>Whether an address inside a text page is one of the unused screen-hole bytes.</summary>
    public static bool IsScreenHole(ushort address, ushort pageBaseAddress = TextPage1BaseAddress)
    {
        if (address < pageBaseAddress || address >= pageBaseAddress + TextPageSize)
            return false;
        return (address - pageBaseAddress) % 0x80 >= 0x78;
    }

    private static void ValidateRow(int row)
    {
        if (row < 0 || row >= Rows)
            throw new ArgumentOutOfRangeException(nameof(row), row, $"Text row must be 0-{Rows - 1}.");
    }

    private static void ValidateColumn(int column)
    {
        if (column < 0 || column >= Columns)
            throw new ArgumentOutOfRangeException(nameof(column), column, $"Text column must be 0-{Columns - 1}.");
    }
}
