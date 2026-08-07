using Highbyte.DotNet6502.Systems.Apple2.DiskImage.Download;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Per-program emulator settings carried by the Download &amp; Run entries, mirroring the C64 list.
/// </summary>
public class Apple2DownloadProgramInfoTests
{
    [Fact]
    public void A_Program_Does_Not_Enable_The_Keyboard_Joystick_Unless_Asked()
    {
        var info = new Apple2DownloadProgramInfo("Test", "https://example.invalid/test.dsk");

        // Turning it on takes keys away from the Apple II keyboard, so it has to be opt-in per
        // entry rather than something every program gets.
        Assert.False(info.KeyboardJoystickEnabled);
    }

    [Fact]
    public void A_Program_Can_Ask_For_The_Keyboard_Joystick()
    {
        var info = new Apple2DownloadProgramInfo(
            "Test",
            "https://example.invalid/test.zip",
            zipEntryName: "test.dsk",
            runMode: Apple2DownloadRunMode.BootDisk,
            keyboardJoystickEnabled: true);

        Assert.True(info.KeyboardJoystickEnabled);
        Assert.Equal(Apple2DownloadRunMode.BootDisk, info.RunMode);
        Assert.True(info.RequiresDisk2Rom);
    }
}
