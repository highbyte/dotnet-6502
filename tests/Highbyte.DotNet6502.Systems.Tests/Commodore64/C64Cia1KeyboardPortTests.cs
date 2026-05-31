using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class C64Cia1KeyboardPortTests
{
    [Fact]
    public void Stop_Key_Is_Visible_On_Port_B_When_Port_A_Drives_Its_Line_Low()
    {
        var c64 = BuildC64();
        c64.Cia1.Keyboard.SetKeysPressed([C64Key.Stop], restorePressed: false, capsLockOn: false);

        c64.Mem.Write(CiaAddr.CIA1_DDRA, 0xFF);
        c64.Mem.Write(CiaAddr.CIA1_DDRB, 0x00);
        c64.Mem.Write(CiaAddr.CIA1_DATAA, 0b0111_1111);

        var portB = c64.Mem.Read(CiaAddr.CIA1_DATAB);

        Assert.Equal(0b0111_1111, portB);
    }

    [Fact]
    public void Stop_Key_Is_Visible_On_Port_A_When_Port_B_Drives_Its_Line_Low()
    {
        var c64 = BuildC64();
        c64.Cia1.Keyboard.SetKeysPressed([C64Key.Stop], restorePressed: false, capsLockOn: false);

        c64.Mem.Write(CiaAddr.CIA1_DDRA, 0x00);
        c64.Mem.Write(CiaAddr.CIA1_DDRB, 0xFF);
        c64.Mem.Write(CiaAddr.CIA1_DATAB, 0b0111_1111);

        var portA = c64.Mem.Read(CiaAddr.CIA1_DATAA);

        Assert.Equal(0b0111_1111, portA);
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
