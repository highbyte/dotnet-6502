using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Oric.Audio;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricMemoryAndBusTests
{
    [Fact]
    public void AtmosRomIsMappedReadOnlyAtC000()
    {
        var rom = Enumerable.Range(0, OricMachine.SystemRomSize).Select(i => (byte)i).ToArray();
        rom[^4] = 0x00;
        rom[^3] = 0xc0;
        var oric = new OricMachine(new(), new NullLoggerFactory(),
            new Dictionary<string, byte[]> { [OricSystemConfig.SystemRomName] = rom });

        Assert.Equal(rom[0], oric.Mem[0xc000]);
        Assert.Equal(rom[^1], oric.Mem[0xffff]);
        oric.Mem[0xc000] = 0x55;
        Assert.Equal(rom[0], oric.Mem[0xc000]);
    }

    [Fact]
    public void ViaRegistersAreMirroredAcrossPageThree()
    {
        var oric = new OricMachine();

        oric.Mem[0x0302] = 0x5a;

        Assert.Equal(0x5a, oric.Mem[0x03f2]);
    }

    [Fact]
    public void ViaControlPinsSelectAndWriteAyRegister()
    {
        var oric = new OricMachine();
        oric.Mem[0x0303] = 0xff;
        oric.Mem[0x0301] = 14;
        oric.Mem[0x030c] = 0xee; // CA2 high, CB2 high: latch address
        oric.Mem[0x030c] = 0xec; // CA2 low, CB2 high: write data
        oric.Mem[0x0301] = 0xaa;

        Assert.Equal(14, oric.Ay.SelectedRegister);
        Assert.Equal(0xaa, oric.Ay.ReadRegister(14));
    }

    [Fact]
    public void AyKeyboardMaskDrivesViaPb3SenseInput()
    {
        var oric = new OricMachine();
        oric.Mem[0x0302] = 0x07; // PB0-PB2 row output; PB3 input
        oric.Mem[0x0300] = 0x00; // row zero
        oric.Ay.WriteRegister(14, 0xfe); // select column bit zero

        oric.Keyboard.SetKeysPressed(new HashSet<HostKey> { HostKey.Digit7 });
        Assert.NotEqual(0, oric.Mem[0x0300] & 0x08);

        oric.Keyboard.Reset();
        Assert.Equal(0, oric.Mem[0x0300] & 0x08);
    }
}
