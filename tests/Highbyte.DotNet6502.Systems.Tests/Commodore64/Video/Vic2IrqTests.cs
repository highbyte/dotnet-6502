using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

public class Vic2IrqTests
{
    [Fact]
    public void Raster_Event_Is_Latched_When_Irq_Mask_Is_Disabled()
    {
        var c64 = BuildC64();
        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 1);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0);

        c64.Vic2.AdvanceRaster(c64.Vic2.Vic2Model.CyclesPerLine);

        Assert.Equal(0x01, c64.Mem.Read(Vic2Addr.VIC_IRQ) & 0x81);
        Assert.False(c64.CPU.CPUInterrupts.IsIRQSourceActive(IRQSource.RasterCompare.ToString()));
    }

    [Fact]
    public void Enabling_And_Disabling_A_Latched_Source_Controls_The_Cpu_Irq_Line()
    {
        var c64 = BuildC64();
        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 1);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0);
        c64.Vic2.AdvanceRaster(c64.Vic2.Vic2Model.CyclesPerLine);

        c64.Mem.Write(Vic2Addr.IRQ_MASK, 1);
        Assert.True(c64.CPU.CPUInterrupts.IsIRQSourceActive(IRQSource.RasterCompare.ToString()));
        Assert.Equal(0x81, c64.Mem.Read(Vic2Addr.VIC_IRQ) & 0x81);

        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0);
        Assert.False(c64.CPU.CPUInterrupts.IsIRQSourceActive(IRQSource.RasterCompare.ToString()));
        Assert.Equal(0x01, c64.Mem.Read(Vic2Addr.VIC_IRQ) & 0x81);
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
