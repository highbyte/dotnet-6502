using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Vic20;

public class Vic20KeyboardRestoreTests
{
    [Fact]
    public void RestoreRelease_Clears_Keyboard_Nmi_Source()
    {
        var vic20 = BuildVic20();

        vic20.Via1.Keyboard.SetKeysPressed([], restorePressed: true, capsLockOn: false);
        Assert.True(vic20.CPU.CPUInterrupts.NMILineEnabled);

        vic20.Via1.Keyboard.SetKeysPressed([], restorePressed: false, capsLockOn: false);

        Assert.False(vic20.CPU.CPUInterrupts.NMILineEnabled);
    }

    [Fact]
    public void Restore_Can_Raise_A_New_Nmi_After_Being_Released()
    {
        var vic20 = BuildVic20();

        vic20.Via1.Keyboard.SetKeysPressed([], restorePressed: true, capsLockOn: false);
        Assert.True(vic20.CPU.CPUInterrupts.NMIPending);

        vic20.CPU.CPUInterrupts.ClearPendingNMI();
        vic20.Via1.Keyboard.SetKeysPressed([], restorePressed: false, capsLockOn: false);
        Assert.False(vic20.CPU.CPUInterrupts.NMILineEnabled);

        vic20.Via1.Keyboard.SetKeysPressed([], restorePressed: true, capsLockOn: false);

        Assert.True(vic20.CPU.CPUInterrupts.NMILineEnabled);
        Assert.True(vic20.CPU.CPUInterrupts.NMIPending);
    }

    [Fact]
    public void Restore_Can_Raise_A_New_Nmi_When_RunStop_Joins_After_Restore()
    {
        var vic20 = BuildVic20();

        vic20.Via1.Keyboard.SetKeysPressed([], restorePressed: true, capsLockOn: false);
        Assert.True(vic20.CPU.CPUInterrupts.NMIPending);

        vic20.CPU.CPUInterrupts.ClearPendingNMI();
        vic20.Via1.Keyboard.SetKeysPressed([Vic20Key.RunStop], restorePressed: true, capsLockOn: false);

        Assert.True(vic20.CPU.CPUInterrupts.NMILineEnabled);
        Assert.True(vic20.CPU.CPUInterrupts.NMIPending);
    }

    private static Highbyte.DotNet6502.Systems.Vic20.Vic20 BuildVic20()
    {
        return new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
    }
}
