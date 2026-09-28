using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

/// <summary>
/// A CPU read of an address nothing answers returns the byte the VIC-II read in the first phase
/// of that cycle, which stays on the data bus: the I/O 1 and I/O 2 areas without a cartridge, and
/// the high four bits of the colour RAM. Per cycle (0-based offsets, PAL): sprite n's pointer
/// access at 57 + 2n (wrapping into the next line for sprites 3-7) followed by its data's middle
/// byte or an idle access; refresh at 10-14; graphics at 15-54; idle accesses elsewhere, which
/// read $3FFF.
/// </summary>
public class Vic2FirstPhaseBusByteTests
{
    private const int CyclesPerLine = 63;
    private const int Line = 20;   // outside the display window: idle state

    private static C64 Build()
    {
        var c64 = C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);
        c64.Mem.Write(Vic2Addr.MEMORY_SETUP, 0x14);          // video matrix at $0400
        for (var n = 0; n < 8; n++)
            c64.Vic2.Vic2Mem[(ushort)(0x07F8 + n)] = (byte)(0x80 + n);   // sprite pointers
        for (var a = 0x3F00; a < 0x3FFF; a++)
            c64.Vic2.Vic2Mem[(ushort)a] = 0x55;                  // what refresh accesses read
        c64.Vic2.Vic2Mem[0x3FFF] = 0xFF;                         // idle accesses
        c64.Vic2.Vic2Mem[0x39FF] = 0xAA;                         // idle graphics accesses with ECM
        return c64;
    }

    private static void PositionAt(C64 c64, int line, int offset)
        => c64.Vic2.AdvanceRaster((ulong)(line * CyclesPerLine + offset) - c64.Vic2.CyclesConsumedCurrentVblank);

    [Theory]
    [InlineData(0, 0x83)]    // sprite 3's pointer access
    [InlineData(1, 0xFF)]    // its data slot, DMA off: idle
    [InlineData(9, 0xFF)]
    [InlineData(10, 0x55)]   // refresh
    [InlineData(14, 0x55)]
    [InlineData(15, 0xFF)]   // graphics, idle state: $3FFF
    [InlineData(54, 0xFF)]
    [InlineData(55, 0xFF)]   // idle
    [InlineData(56, 0xFF)]
    [InlineData(57, 0x80)]   // sprite 0's pointer access
    [InlineData(61, 0x82)]   // sprite 2's
    [InlineData(62, 0xFF)]   // its data slot
    public void Unconnected_io_reads_what_the_vic_ii_fetched_in_the_cycles_first_phase(int offset, byte expected)
    {
        var c64 = Build();
        PositionAt(c64, Line, offset);
        Assert.Equal(expected, c64.Mem.Read(0xDE00));
        Assert.Equal(expected, c64.Mem.Read(0xDFFF));
    }

    [Theory]
    [InlineData(15, 0xAA)]   // graphics accesses read $39FF with ECM set
    [InlineData(55, 0xFF)]   // the other idle accesses still read $3FFF
    public void Idle_graphics_accesses_follow_ecm_and_the_other_idle_accesses_do_not(int offset, byte expected)
    {
        var c64 = Build();
        c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, 0x4B);
        PositionAt(c64, Line, offset);
        Assert.Equal(expected, c64.Mem.Read(0xDE00));
    }

    [Fact]
    public void Refresh_accesses_count_down_from_ff_at_line_0()
    {
        var c64 = Build();
        c64.Vic2.Vic2Mem[0x3FFF - 5 * Line - 2] = 0x12;         // line 20's third refresh access
        PositionAt(c64, Line, 12);
        Assert.Equal(0x12, c64.Mem.Read(0xDE00));
    }

    [Fact]
    public void A_sprite_with_dma_on_shows_the_middle_byte_of_its_row_in_its_data_slot()
    {
        var c64 = Build();
        c64.Vic2.Vic2Mem[0x80 * 64 + 1] = 0x3C;                   // sprite 0, row 0, middle byte
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, Line);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        PositionAt(c64, Line, 58);                                // the compare started the DMA in cycle 55
        Assert.Equal(0x3C, c64.Mem.Read(0xDE00));
    }

    [Fact]
    public void Graphics_accesses_in_display_state_read_the_character_data()
    {
        // YSCROLL 3 with the display on: line 51 is a bad line, display state, row counter 0; the
        // first column's character 1 has its data at $3008 in a character set at $3000.
        var c64 = Build();
        c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, 0x1B);
        c64.Mem.Write(Vic2Addr.MEMORY_SETUP, 0x1C);               // character set at $3000
        c64.Vic2.Vic2Mem[0x0400] = 0x01;
        c64.Vic2.Vic2Mem[0x3008] = 0x5A;
        PositionAt(c64, 51, 15);
        Assert.Equal(0x5A, c64.Mem.Read(0xDE00));
    }

    [Fact]
    public void Colour_ram_reads_have_the_first_phase_byte_in_their_high_four_bits()
    {
        var c64 = Build();
        c64.Mem.Write(0xD800, 0x07);
        c64.Vic2.Vic2Mem[0x07F8] = 0xA0;                          // sprite 0's pointer
        PositionAt(c64, Line, 57);
        Assert.Equal(0xA7, c64.Mem.Read(0xD800));
    }
}
