using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.TimerAndPeripheral;

public class CiaMirroringTests
{
    [Fact]
    public void Cia1_Interrupt_Control_Register_Is_Mirrored_To_Dcfd()
    {
        var c64 = BuildC64();
        c64.Cia1.TimerBLOStore(0, 1);
        c64.Cia1.TimerBHIStore(0, 0);
        c64.Cia1.InterruptControlStore(0, 0x82);
        c64.Cia1.TimerBControlStore(0, 0x19);

        c64.Cia1.ProcessTimers(2);

        Assert.Equal(0x82, c64.Mem.Read(0xDCFD));
        Assert.Equal(0x00, c64.Mem.Read(CiaAddr.CIA1_CIAICR));
    }

    [Fact]
    public void Cia1_Data_Direction_Register_Writes_Are_Mirrored()
    {
        var c64 = BuildC64();

        c64.Mem.Write(0xDCF2, 0xA5);

        Assert.Equal(0xA5, c64.Mem.Read(CiaAddr.CIA1_DDRA));
        Assert.Equal(0xA5, c64.Mem.Read(0xDCF2));
    }

    [Fact]
    public void Cia2_Data_Port_Writes_Are_Mirrored()
    {
        var c64 = BuildC64();

        c64.Mem.Write(0xDDF0, 0x03);

        Assert.Equal(0xC3, c64.Mem.Read(CiaAddr.CIA2_DATAA));
        Assert.Equal(0xC3, c64.Mem.Read(0xDDF0));
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
