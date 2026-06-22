using System.Buffers.Binary;
using System.Text;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.Crt;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Cartridge;

public class C64CrtTests
{
    [Fact]
    public void Parser_Reads_Header_And_Chip_Fields()
    {
        var bytes = BuildCrt(
            hardwareType: 0,
            exromHigh: false,
            gameHigh: true,
            name: "TEST CART",
            headerLength: 0x50,
            chips: [new Chip(0, 0x8000, Filled(0x2000, 0x42))]);

        var image = C64CrtParser.Parse(bytes);

        Assert.Equal((uint)0x50, image.Header.HeaderLength);
        Assert.Equal((ushort)0x0100, image.Header.Version);
        Assert.Equal((ushort)0, image.Header.HardwareType);
        Assert.False(image.Header.ExromHigh);
        Assert.True(image.Header.GameHigh);
        Assert.Equal("TEST CART", image.Header.Name);
        var chip = Assert.Single(image.Chips);
        Assert.Equal(C64CrtChipType.Rom, chip.Type);
        Assert.Equal((ushort)0, chip.Bank);
        Assert.Equal((ushort)0x8000, chip.LoadAddress);
        Assert.Equal(0x2000, chip.Data.Length);
        Assert.Equal(0x42, chip.Data[0]);
    }

    [Theory]
    [InlineData("bad-signature")]
    [InlineData("truncated-chip")]
    [InlineData("bad-packet-length")]
    [InlineData("bad-chip-type")]
    public void Parser_Rejects_Malformed_Images(string corruption)
    {
        var bytes = BuildCrt(
            hardwareType: 0,
            exromHigh: false,
            gameHigh: true,
            chips: [new Chip(0, 0x8000, Filled(0x2000, 0x42))]);

        switch (corruption)
        {
            case "bad-signature":
                bytes[0] = (byte)'X';
                break;
            case "truncated-chip":
                Array.Resize(ref bytes, bytes.Length - 1);
                break;
            case "bad-packet-length":
                BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0x44, 4), 8);
                break;
            case "bad-chip-type":
                BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(0x48, 2), 99);
                break;
        }

        Assert.Throws<C64CrtImageException>(() => C64CrtParser.Parse(bytes));
    }

    [Fact]
    public void Factory_Creates_Generic_8K_Cartridge()
    {
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: 0,
            exromHigh: false,
            gameHigh: true,
            chips: [new Chip(0, 0x8000, Filled(0x2000, 0x42))]));

        var cartridge = C64CrtCartridgeFactory.Create(image);

        Assert.Equal(new C64CartridgeLines(GameHigh: true, ExromHigh: false), cartridge.Lines);
        Assert.True(cartridge.HasROML);
        Assert.False(cartridge.HasROMH);
        Assert.Equal(0x42, cartridge.ReadROML(0x8000));
    }

    [Fact]
    public void Factory_Creates_Generic_16K_From_One_Combined_Chip()
    {
        var data = Filled(0x4000, 0x42);
        Array.Fill(data, (byte)0x84, 0x2000, 0x2000);
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: 0,
            exromHigh: false,
            gameHigh: false,
            chips: [new Chip(0, 0x8000, data)]));

        var cartridge = C64CrtCartridgeFactory.Create(image);

        Assert.Equal(0x42, cartridge.ReadROML(0x8000));
        Assert.Equal(0x84, cartridge.ReadROMH(0xA000));
    }

    [Fact]
    public void Factory_Creates_Generic_Ultimax_Cartridge()
    {
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: 0,
            exromHigh: true,
            gameHigh: false,
            chips:
            [
                new Chip(0, 0x8000, Filled(0x2000, 0x42)),
                new Chip(0, 0xE000, Filled(0x2000, 0x84)),
            ]));

        var cartridge = C64CrtCartridgeFactory.Create(image);

        Assert.Equal(new C64CartridgeLines(GameHigh: false, ExromHigh: true), cartridge.Lines);
        Assert.Equal(0x42, cartridge.ReadROML(0x8000));
        Assert.Equal(0x84, cartridge.ReadROMH(0xE000));
    }

    [Fact]
    public void Factory_Rejects_Chip_Data_Outside_Selected_Rom_Windows()
    {
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: 0,
            exromHigh: false,
            gameHigh: true,
            chips:
            [
                new Chip(0, 0x8000, Filled(0x2000, 0x42)),
                new Chip(0, 0xA000, Filled(0x2000, 0x84)),
            ]));

        var exception = Assert.Throws<C64CrtImageException>(
            () => C64CrtCartridgeFactory.Create(image));

        Assert.Contains("outside the cartridge ROM windows", exception.Message);
    }

    [Fact]
    public void Factory_Creates_MagicDesk_Cartridge_With_Banked_Rom()
    {
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.MagicDesk,
            exromHigh: false,
            gameHigh: true,
            name: "MAGIC DESK TEST",
            chips:
            [
                new Chip(0, 0x8000, Filled(0x2000, 0x10)),
                new Chip(1, 0x8000, Filled(0x2000, 0x11)),
                new Chip(2, 0x8000, Filled(0x2000, 0x12)),
                new Chip(3, 0x8000, Filled(0x2000, 0x13)),
            ]));

        var cartridge = Assert.IsType<C64MagicDeskCartridge>(
            C64CrtCartridgeFactory.Create(image));

        Assert.Equal("MAGIC DESK TEST", cartridge.Name);
        Assert.Equal(new C64CartridgeLines(GameHigh: true, ExromHigh: false), cartridge.Lines);
        Assert.Equal((ushort)0, cartridge.CurrentBank);
        Assert.Equal(0x10, cartridge.ReadROML(0x8000));
        Assert.False(cartridge.HandlesIORead(0xDE00));
        Assert.True(cartridge.HandlesIOWrite(0xDE00));

        cartridge.WriteIO(0xDE00, 2);

        Assert.Equal((ushort)2, cartridge.CurrentBank);
        Assert.Equal(0x12, cartridge.ReadROML(0x8000));
    }

    [Theory]
    [InlineData("duplicate-bank")]
    [InlineData("invalid-address")]
    [InlineData("invalid-size")]
    [InlineData("bank-too-high")]
    public void Factory_Rejects_Invalid_MagicDesk_Images(string invalidShape)
    {
        Chip[] chips = invalidShape switch
        {
            "duplicate-bank" =>
            [
                new Chip(0, 0x8000, Filled(0x2000, 0x10)),
                new Chip(0, 0x8000, Filled(0x2000, 0x11)),
            ],
            "invalid-address" => [new Chip(0, 0x9000, Filled(0x2000, 0x10))],
            "invalid-size" => [new Chip(0, 0x8000, Filled(0x1000, 0x10))],
            "bank-too-high" => [new Chip(128, 0x8000, Filled(0x2000, 0x10))],
            _ => throw new ArgumentOutOfRangeException(nameof(invalidShape)),
        };
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.MagicDesk,
            exromHigh: false,
            gameHigh: true,
            chips: chips));

        Assert.Throws<C64CrtImageException>(
            () => C64CrtCartridgeFactory.Create(image));
    }

    [Fact]
    public void MagicDesk_Attach_Switches_Banks_And_Can_Disable_And_Reenable_Itself()
    {
        var c64 = BuildC64();
        c64.RAM[0x8000] = 0x44;
        c64.IO[0x0E00] = 0x5A;
        var crt = BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.MagicDesk,
            exromHigh: false,
            gameHigh: true,
            name: "MAGIC DESK TEST",
            chips:
            [
                new Chip(0, 0x8000, Filled(0x2000, 0x10)),
                new Chip(1, 0x8000, Filled(0x2000, 0x11)),
                new Chip(2, 0x8000, Filled(0x2000, 0x12)),
                new Chip(3, 0x8000, Filled(0x2000, 0x13)),
            ]);

        var result = c64.AttachCrtImage(crt, "magic-desk.crt");

        Assert.Equal((ushort)C64CrtHardwareType.MagicDesk, result.HardwareType);
        Assert.Equal(0x10, c64.Mem.Read(0x8000));
        Assert.Equal(0x5A, c64.Mem.Read(0xDE00));

        c64.Mem.Write(0xDE00, 2);

        Assert.Equal(0x12, c64.Mem.Read(0x8000));

        c64.Mem.Write(0xDE00, 0x80);

        Assert.Equal(C64CartridgeLines.Released, c64.CartridgeSlot.Lines);
        Assert.Equal(0x44, c64.Mem.Read(0x8000));

        c64.Mem.Write(0xDE00, 1);

        Assert.Equal(new C64CartridgeLines(GameHigh: true, ExromHigh: false), c64.CartridgeSlot.Lines);
        Assert.Equal(0x11, c64.Mem.Read(0x8000));
    }

    [Fact]
    public void Factory_Creates_Ocean_16K_Cartridge_With_Mirrored_Banked_Rom()
    {
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.Ocean,
            exromHigh: false,
            gameHigh: false,
            name: "OCEAN TEST",
            chips: CreateBankChips(4)));

        var cartridge = Assert.IsType<C64OceanCartridge>(
            C64CrtCartridgeFactory.Create(image));

        Assert.False(cartridge.UseEightKMode);
        Assert.Equal(new C64CartridgeLines(GameHigh: false, ExromHigh: false), cartridge.Lines);
        Assert.True(cartridge.HasROML);
        Assert.True(cartridge.HasROMH);
        Assert.False(cartridge.HandlesIORead(0xDE00));
        Assert.True(cartridge.HandlesIOWrite(0xDE00));
        Assert.Equal(0x10, cartridge.ReadROML(0x8000));
        Assert.Equal(0x10, cartridge.ReadROMH(0xA000));

        cartridge.WriteIO(0xDE00, 0x82);

        Assert.Equal((ushort)2, cartridge.CurrentBank);
        Assert.Equal(0x12, cartridge.ReadROML(0x8000));
        Assert.Equal(0x12, cartridge.ReadROMH(0xA000));
    }

    [Fact]
    public void Factory_Creates_Ocean_512K_Cartridge_In_8K_Mode()
    {
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.Ocean,
            exromHigh: false,
            gameHigh: true,
            chips: CreateBankChips(64)));

        var cartridge = Assert.IsType<C64OceanCartridge>(
            C64CrtCartridgeFactory.Create(image));

        Assert.True(cartridge.UseEightKMode);
        Assert.Equal(new C64CartridgeLines(GameHigh: true, ExromHigh: false), cartridge.Lines);
        Assert.False(cartridge.HasROMH);

        cartridge.WriteIO(0xDE00, 63);

        Assert.Equal((ushort)63, cartridge.CurrentBank);
        Assert.Equal(0x4F, cartridge.ReadROML(0x8000));
    }

    [Theory]
    [InlineData("missing-bank")]
    [InlineData("non-power-of-two")]
    [InlineData("invalid-address")]
    [InlineData("invalid-size")]
    [InlineData("bank-too-high")]
    public void Factory_Rejects_Invalid_Ocean_Images(string invalidShape)
    {
        Chip[] chips = invalidShape switch
        {
            "missing-bank" =>
            [
                new Chip(0, 0x8000, Filled(0x2000, 0x10)),
                new Chip(2, 0x8000, Filled(0x2000, 0x12)),
            ],
            "non-power-of-two" => CreateBankChips(3),
            "invalid-address" => [new Chip(0, 0x9000, Filled(0x2000, 0x10))],
            "invalid-size" => [new Chip(0, 0x8000, Filled(0x1000, 0x10))],
            "bank-too-high" => [new Chip(64, 0x8000, Filled(0x2000, 0x10))],
            _ => throw new ArgumentOutOfRangeException(nameof(invalidShape)),
        };
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.Ocean,
            exromHigh: false,
            gameHigh: false,
            chips: chips));

        Assert.Throws<C64CrtImageException>(
            () => C64CrtCartridgeFactory.Create(image));
    }

    [Fact]
    public void Ocean_Attach_Maps_And_Switches_Mirrored_Rom_Banks()
    {
        var c64 = BuildC64();
        c64.IO[0x0E00] = 0x5A;
        var crt = BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.Ocean,
            exromHigh: false,
            gameHigh: false,
            name: "OCEAN TEST",
            chips: CreateBankChips(4));

        var result = c64.AttachCrtImage(crt, "ocean.crt");

        Assert.Equal((ushort)C64CrtHardwareType.Ocean, result.HardwareType);
        Assert.Equal(0x10, c64.Mem.Read(0x8000));
        Assert.Equal(0x10, c64.Mem.Read(0xA000));
        Assert.Equal(0x5A, c64.Mem.Read(0xDE00));

        c64.Mem.Write(0xDE00, 3);

        Assert.Equal(0x13, c64.Mem.Read(0x8000));
        Assert.Equal(0x13, c64.Mem.Read(0xA000));
    }

    [Fact]
    public void Unsupported_Hardware_Type_Does_Not_Replace_Current_Cartridge()
    {
        var c64 = BuildC64();
        var current = new C64RomCartridge(Filled(0x2000, 0x11), name: "Current");
        c64.AttachCartridge(current);
        var unsupported = BuildCrt(
            hardwareType: 32,
            exromHigh: false,
            gameHigh: true,
            chips: [new Chip(0, 0x8000, Filled(0x2000, 0x42))]);

        Assert.Throws<C64UnsupportedCrtHardwareException>(
            () => c64.AttachCrtImage(unsupported, "unsupported.crt"));

        Assert.Same(current, c64.CartridgeSlot.AttachedCartridge);
    }

    [Fact]
    public void AttachCrtImage_Replaces_Cartridge_And_Hard_Resets_From_Cartridge_Vector()
    {
        var c64 = BuildC64();
        var romh = Filled(0x2000, 0x84);
        romh[0x1FFC] = 0x34;
        romh[0x1FFD] = 0x12;
        var crt = BuildCrt(
            hardwareType: 0,
            exromHigh: true,
            gameHigh: false,
            name: "ULTIMAX TEST",
            chips:
            [
                new Chip(0, 0x8000, Filled(0x2000, 0x42)),
                new Chip(0, 0xE000, romh),
            ]);

        var result = c64.AttachCrtImage(crt, "test.crt");

        Assert.Equal("ULTIMAX TEST", result.CartridgeName);
        Assert.Equal("test.crt", result.SourceName);
        Assert.Equal((ushort)0x1234, c64.CPU.PC);
        Assert.Equal(0x84, c64.Mem.Read(0xE000));
        Assert.Same(result, c64.AttachedCartridgeImage);
    }

    private static C64 BuildC64()
        => C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
        }, NullLoggerFactory.Instance);

    private static byte[] BuildCrt(
        ushort hardwareType,
        bool exromHigh,
        bool gameHigh,
        Chip[] chips,
        string name = "TEST",
        int headerLength = 0x40)
    {
        var size = headerLength + chips.Sum(chip => 0x10 + chip.Data.Length);
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes("C64 CARTRIDGE   ").CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0x10, 4), (uint)headerLength);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(0x14, 2), 0x0100);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(0x16, 2), hardwareType);
        bytes[0x18] = exromHigh ? (byte)1 : (byte)0;
        bytes[0x19] = gameHigh ? (byte)1 : (byte)0;
        Encoding.ASCII.GetBytes(name).CopyTo(bytes, 0x20);

        var offset = headerLength;
        foreach (var chip in chips)
        {
            Encoding.ASCII.GetBytes("CHIP").CopyTo(bytes, offset);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 4, 4), (uint)(0x10 + chip.Data.Length));
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 8, 2), (ushort)C64CrtChipType.Rom);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 10, 2), chip.Bank);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 12, 2), chip.Address);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 14, 2), (ushort)chip.Data.Length);
            chip.Data.CopyTo(bytes, offset + 0x10);
            offset += 0x10 + chip.Data.Length;
        }
        return bytes;
    }

    private static byte[] Filled(int length, byte value)
    {
        var data = new byte[length];
        Array.Fill(data, value);
        return data;
    }

    private static Chip[] CreateBankChips(int count)
        => Enumerable.Range(0, count)
            .Select(bank => new Chip(
                (ushort)bank,
                0x8000,
                Filled(0x2000, (byte)(0x10 + bank))))
            .ToArray();

    private sealed record Chip(ushort Bank, ushort Address, byte[] Data);
}
