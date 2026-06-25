using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Cartridge;

public class C64RomCartridgeTests
{
    [Fact]
    public void EightK_Cartridge_Maps_ROML_And_Writes_Through_To_Underlying_RAM()
    {
        var c64 = BuildC64();
        c64.RAM[0x8000] = 0x11;
        var roml = CreateRom(0x42);

        c64.AttachCartridge(new C64RomCartridge(roml, name: "8K test"));

        Assert.Equal(15, c64.CurrentBank);
        Assert.Equal(0x42, c64.Mem.Read(0x8000));

        c64.Mem.Write(0x8000, 0x77);

        Assert.Equal(0x77, c64.RAM[0x8000]);
        Assert.Equal(0x42, c64.Mem.Read(0x8000));

        c64.DetachCartridge();

        Assert.Equal(31, c64.CurrentBank);
        Assert.Equal(0x77, c64.Mem.Read(0x8000));
    }

    [Fact]
    public void SixteenK_Cartridge_Maps_ROML_And_ROMH()
    {
        var c64 = BuildC64();
        var roml = CreateRom(0x42);
        var romh = CreateRom(0x84);

        c64.AttachCartridge(new C64RomCartridge(roml, romh, "16K test"));

        Assert.Equal(7, c64.CurrentBank);
        Assert.Equal(0x42, c64.Mem.Read(0x8000));
        Assert.Equal(0x84, c64.Mem.Read(0xA000));
        Assert.Equal(0x42, c64.Mem.Read(0x9FFF));
        Assert.Equal(0x84, c64.Mem.Read(0xBFFF));
    }

    [Fact]
    public void SixteenK_Cartridge_Respects_Cpu_Port_Control_Of_Rom_Windows()
    {
        var c64 = BuildC64();
        c64.RAM[0x8000] = 0x11;
        c64.RAM[0xA000] = 0x22;
        c64.AttachCartridge(new C64RomCartridge(CreateRom(0x42), CreateRom(0x84)));

        c64.Mem.Write(0x0001, 0x06);

        Assert.Equal(6, c64.CurrentBank);
        Assert.Equal(0x11, c64.Mem.Read(0x8000));
        Assert.Equal(0x84, c64.Mem.Read(0xA000));
    }

    [Theory]
    [InlineData(0x1FFF, false)]
    [InlineData(0x2000, true)]
    [InlineData(0x2001, false)]
    public void Cartridge_Validates_Rom_Window_Size(int size, bool isValid)
    {
        var roml = new byte[size];

        if (isValid)
            _ = new C64RomCartridge(roml);
        else
            Assert.Throws<ArgumentException>(() => new C64RomCartridge(roml));
    }

    private static C64 BuildC64()
        => C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
        }, NullLoggerFactory.Instance);

    private static byte[] CreateRom(byte value)
    {
        var rom = new byte[C64RomCartridge.RomWindowSize];
        Array.Fill(rom, value);
        return rom;
    }
}
