using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

public class Vic2IrqTests
{
    private static readonly string RasterCompareIrqSource = Vic2IRQ.GetInterruptSourceName(IRQSource.RasterCompare);

    [Fact]
    public void Raster_Event_Is_Latched_When_Irq_Mask_Is_Disabled()
    {
        var c64 = BuildC64();
        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 1);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0);

        c64.Vic2.AdvanceRaster(c64.Vic2.Vic2Model.CyclesPerLine);

        Assert.Equal(0x01, c64.Mem.Read(Vic2Addr.VIC_IRQ) & 0x81);
        Assert.False(c64.CPU.CPUInterrupts.IsIRQSourceActive(RasterCompareIrqSource));
    }

    [Fact]
    public void Enabling_And_Disabling_A_Latched_Source_Controls_The_Cpu_Irq_Line()
    {
        var c64 = BuildC64();
        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 1);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0);
        c64.Vic2.AdvanceRaster(c64.Vic2.Vic2Model.CyclesPerLine);

        c64.Mem.Write(Vic2Addr.IRQ_MASK, 1);
        Assert.True(c64.CPU.CPUInterrupts.IsIRQSourceActive(RasterCompareIrqSource));
        Assert.Equal(0x81, c64.Mem.Read(Vic2Addr.VIC_IRQ) & 0x81);

        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0);
        Assert.False(c64.CPU.CPUInterrupts.IsIRQSourceActive(RasterCompareIrqSource));
        Assert.Equal(0x01, c64.Mem.Read(Vic2Addr.VIC_IRQ) & 0x81);
    }

    [Fact]
    public void Writing_the_current_line_as_the_compare_value_raises_the_interrupt_at_once()
    {
        var c64 = BuildC64();
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 1);
        c64.Vic2.AdvanceRaster(10 * c64.Vic2.Vic2Model.CyclesPerLine + 8);

        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 10);

        Assert.Equal(0x81, c64.Mem.Read(Vic2Addr.VIC_IRQ) & 0x81);
    }

    [Fact]
    public void Keeping_the_compare_matched_across_lines_raises_no_further_interrupt()
    {
        // The interrupt is raised when the comparison goes from non-match to match. A program that
        // moves the compare value to the next line in every line's last cycle keeps it matched, so
        // no interrupt follows (VICE's rasterirq/rasterirq_hold); once the comparison has failed,
        // the next match raises one again.
        var c64 = BuildC64();
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 1);
        c64.Vic2.AdvanceRaster(10 * cyclesPerLine + 8);
        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 10);
        c64.Mem.Write(Vic2Addr.VIC_IRQ, 1);   // acknowledge the interrupt this raised

        for (var line = 10; line < 20; line++)
        {
            c64.Vic2.AdvanceRaster(cyclesPerLine - 9);   // the line's last cycle
            c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, (byte)(line + 1));
            c64.Vic2.AdvanceRaster(9);
            Assert.Equal(0x00, c64.Mem.Read(Vic2Addr.VIC_IRQ) & 0x81);
        }

        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 22);   // no match on line 20
        c64.Vic2.AdvanceRaster(2 * cyclesPerLine);
        Assert.Equal(0x81, c64.Mem.Read(Vic2Addr.VIC_IRQ) & 0x81);
    }

    private static C64 BuildC64()
    {
        return C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL"
        }, NullLoggerFactory.Instance);
    }
}
