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
    public void LeavingAyAddressLatchDoesNotWriteRegisterNumberAsData()
    {
        var oric = new OricMachine();
        oric.Ay.WriteRegister(8, 12);
        oric.Mem[0x0301] = 8;
        oric.Mem[0x030c] = 0xee; // latch address: CA2=1, CB2=1
        oric.Mem[0x030c] = 0xcc; // inactive: CA2=0, CB2=0, one PCR write

        Assert.Equal(12, oric.Ay.ReadRegister(8));
        oric.Mem[0x0301] = 10;
        oric.Mem[0x030c] = 0xec; // data write strobe
        oric.Mem[0x030c] = 0xcc;
        Assert.Equal(10, oric.Ay.ReadRegister(8));
    }

    [Fact]
    public void LeavingAyAddressLatchDoesNotRetriggerEnvelope()
    {
        var oric = new OricMachine();
        oric.Ay.WriteRegister(7, 0x3f);
        oric.Ay.WriteRegister(8, 16);
        oric.Ay.WriteRegister(11, 1);
        oric.Ay.WriteRegister(13, 0);
        oric.Ay.AdvanceCycles(1000, new float[64]); // decay has finished
        oric.Mem[0x0301] = 13;
        oric.Mem[0x030c] = 0xee;
        oric.Mem[0x030c] = 0xcc;

        Assert.Equal(0, oric.Ay.ReadRegister(13));
        var samples = new float[64];
        var count = oric.Ay.AdvanceCycles(1000, samples);
        Assert.All(samples[..count], sample => Assert.Equal(0f, sample));
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
