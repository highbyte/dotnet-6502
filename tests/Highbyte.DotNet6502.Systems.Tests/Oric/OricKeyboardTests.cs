using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Oric.Input;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricKeyboardTests
{
    [Fact]
    public void AltMapsToAtmosFunctKey()
    {
        var keyboard = new OricKeyboard();

        keyboard.SetKeysPressed(new HashSet<HostKey> { HostKey.AltLeft });

        Assert.Equal(0, keyboard.ReadRow(5) & 0x10);
    }

    [Fact]
    public void HostFunctionKeysAreNotPartOfAtmosMatrix()
    {
        var keyboard = new OricKeyboard();
        var hostFunctionKeys = new HashSet<HostKey>
        {
            HostKey.F1, HostKey.F2, HostKey.F3, HostKey.F4, HostKey.F5, HostKey.F6,
            HostKey.F7, HostKey.F8, HostKey.F9, HostKey.F10, HostKey.F11, HostKey.F12,
        };

        keyboard.SetKeysPressed(hostFunctionKeys);

        for (var row = 0; row < 8; row++)
            Assert.Equal(0xff, keyboard.ReadRow(row));
    }
}
