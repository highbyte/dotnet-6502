using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

/// <summary>
/// A CPU write into the VIC-II's bank is seen by the chip's fetches from the cycle after the
/// write on: the chip reads in a cycle's first clock phase and the CPU writes in its second, so
/// the fetch of the write's own cycle still sees the old byte, and so do the fetches of the
/// instruction's earlier cycles. A byte rewritten in the middle of a line therefore reaches the
/// screen from the column after the write's cycle, as on hardware.
/// </summary>
public class C64VideoMemoryWriteTimingTests
{
    private const ushort Start = 0x1000;

    [Theory]
    [InlineData(0, 0x3FFF)]
    [InlineData(1, 0x7FFF)]   // the last byte of each bank: the idle byte's address there
    [InlineData(2, 0xBFFF)]
    [InlineData(3, 0xFFFF)]
    public void A_write_to_the_idle_byte_shows_from_the_column_after_the_writes_cycle(int bank, int idleByteAddress)
    {
        // YSCROLL 7: the first bad line is 55, so lines 51-54 are idle lines inside the display
        // window and show the byte at $3FFF, black for its set bits, in every column. The program
        // starts in cycle index 24 of line 52: LDA #$FF (24, 25), STA to it (26, 27, 28, the write
        // in 29), then a loop for the rest of the frame. The g-access of index 29 (the chip's cycle
        // 30, column 14) reads before the write, that of index 30 (column 15) after it.
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
            RenderProviderType = typeof(Vic2Rasterizer),
            Vic2RasterizerPerLineSprites = true,
        }, NullLoggerFactory.Instance);
        c64.Mem.StoreData(Start, [0xA9, 0xFF, 0x8D, (byte)(idleByteAddress & 0xFF), (byte)(idleByteAddress >> 8), 0x4C, 0x05, 0x10]);
        c64.CPU.PC = Start;
        c64.Mem.Write(0xDD02, 0x3F);                    // CIA 2 port A's bank bits as outputs
        c64.Mem.Write(0xDD00, (byte)(3 - bank));        // the bank, inverted
        c64.Mem.Write(0xD016, 0xC8);
        c64.Mem.Write(0xD021, 6);
        c64.Mem.Write(0xD011, 0x1F);
        c64.Vic2.Vic2Mem[0x3FFF] = 0x00;
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(52 * cyclesPerLine + 24);

        c64.ExecuteOneFrame();

        var rasterizer = (Vic2Rasterizer)c64.RenderProvider!;
        var foreground = rasterizer.CurrentFrontLayerBuffers[1].ToArray();
        var layout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        var width = c64.Screen.VisibleWidth;
        var y = layout.Screen.Start.Y + 52 - 51;
        uint Fg(int column, int pixel) => foreground[y * width + layout.Screen.Start.X + column * 8 + pixel];

        Assert.Equal(0u, Fg(0, 0));          // the old byte, blank, before the write
        Assert.Equal(0u, Fg(11, 0));         // the instruction's own earlier cycles fetch the old byte
        Assert.Equal(0u, Fg(14, 0));         // the write's cycle: the fetch comes first
        Assert.NotEqual(0u, Fg(15, 0));      // from the cycle after the write: $FF, black
        Assert.NotEqual(0u, Fg(39, 7));
    }
}
