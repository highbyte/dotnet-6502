using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class C64KeyboardRestoreTests
{
    [Fact]
    public void RestoreRelease_Clears_Keyboard_Nmi_Source()
    {
        var c64 = BuildC64();

        c64.Cia1.Keyboard.SetKeysPressed([], restorePressed: true, capsLockOn: false);
        Assert.True(c64.CPU.CPUInterrupts.NMILineEnabled);

        c64.Cia1.Keyboard.SetKeysPressed([], restorePressed: false, capsLockOn: false);

        Assert.False(c64.CPU.CPUInterrupts.NMILineEnabled);
    }

    [Fact]
    public void Restore_Can_Raise_A_New_Nmi_After_Being_Released()
    {
        var c64 = BuildC64();

        c64.Cia1.Keyboard.SetKeysPressed([], restorePressed: true, capsLockOn: false);
        Assert.True(c64.CPU.CPUInterrupts.NMIPending);

        c64.CPU.CPUInterrupts.ClearPendingNMI();
        c64.Cia1.Keyboard.SetKeysPressed([], restorePressed: false, capsLockOn: false);
        Assert.False(c64.CPU.CPUInterrupts.NMILineEnabled);

        c64.Cia1.Keyboard.SetKeysPressed([], restorePressed: true, capsLockOn: false);

        Assert.True(c64.CPU.CPUInterrupts.NMILineEnabled);
        Assert.True(c64.CPU.CPUInterrupts.NMIPending);
    }

    [Fact]
    public void Restore_Can_Raise_A_New_Nmi_When_RunStop_Joins_After_Restore()
    {
        var c64 = BuildC64();

        c64.Cia1.Keyboard.SetKeysPressed([], restorePressed: true, capsLockOn: false);
        Assert.True(c64.CPU.CPUInterrupts.NMIPending);

        c64.CPU.CPUInterrupts.ClearPendingNMI();
        c64.Cia1.Keyboard.SetKeysPressed([C64Key.Stop], restorePressed: true, capsLockOn: false);

        Assert.True(c64.CPU.CPUInterrupts.NMILineEnabled);
        Assert.True(c64.CPU.CPUInterrupts.NMIPending);
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
