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
        c64.IO[0x0E00] = 0x5A;
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
        c64.IO[0x0E00] = 0x5A;
        Assert.Equal(0x5A, c64.Mem.Read(0xDE00));

        c64.Mem.Write(0xDE00, 3);

        Assert.Equal(0x13, c64.Mem.Read(0x8000));
        Assert.Equal(0x13, c64.Mem.Read(0xA000));
    }

    [Fact]
    public void Factory_Creates_EpyxFastLoad_Cartridge_With_Timed_Rom_And_IO2_Page()
    {
        var rom = Filled(0x2000, 0x42);
        rom[0x1F34] = 0x84;
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.EpyxFastLoad,
            exromHigh: false,
            gameHigh: true,
            name: "EPYX FASTLOAD TEST",
            chips: [new Chip(0, 0x8000, rom)]));

        var cartridge = Assert.IsType<C64EpyxFastLoadCartridge>(
            C64CrtCartridgeFactory.Create(image));

        Assert.Equal("EPYX FASTLOAD TEST", cartridge.Name);
        Assert.True(cartridge.IsRomEnabled);
        Assert.Equal(new C64CartridgeLines(GameHigh: true, ExromHigh: false), cartridge.Lines);
        Assert.Equal(0x42, cartridge.ReadROML(0x8000));
        Assert.Equal(C64EpyxFastLoadCartridge.RomEnableCycles, cartridge.CyclesUntilDisabled);
        Assert.Equal(0x84, cartridge.ReadIO(0xDF34));

        cartridge.Tick(C64EpyxFastLoadCartridge.RomEnableCycles - 1);

        Assert.True(cartridge.IsRomEnabled);

        cartridge.Tick(1);

        Assert.False(cartridge.IsRomEnabled);
        Assert.Equal(C64CartridgeLines.Released, cartridge.Lines);
        Assert.Equal(0x84, cartridge.ReadIO(0xDF34));

        Assert.Equal(0, cartridge.ReadIO(0xDE00));
        Assert.True(cartridge.IsRomEnabled);
        Assert.Equal(C64EpyxFastLoadCartridge.RomEnableCycles, cartridge.CyclesUntilDisabled);
    }

    [Theory]
    [InlineData("multiple-chips")]
    [InlineData("wrong-bank")]
    [InlineData("wrong-address")]
    [InlineData("wrong-size")]
    public void Factory_Rejects_Invalid_EpyxFastLoad_Images(string invalidShape)
    {
        Chip[] chips = invalidShape switch
        {
            "multiple-chips" =>
            [
                new Chip(0, 0x8000, Filled(0x2000, 0x10)),
                new Chip(0, 0x8000, Filled(0x2000, 0x11)),
            ],
            "wrong-bank" => [new Chip(1, 0x8000, Filled(0x2000, 0x10))],
            "wrong-address" => [new Chip(0, 0xA000, Filled(0x2000, 0x10))],
            "wrong-size" => [new Chip(0, 0x8000, Filled(0x1000, 0x10))],
            _ => throw new ArgumentOutOfRangeException(nameof(invalidShape)),
        };
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.EpyxFastLoad,
            exromHigh: false,
            gameHigh: true,
            chips: chips));

        Assert.Throws<C64CrtImageException>(
            () => C64CrtCartridgeFactory.Create(image));
    }

    [Fact]
    public void EpyxFastLoad_Attach_Times_Out_And_IO1_Read_Reenables_Rom()
    {
        var c64 = BuildC64();
        c64.RAM[0x8000] = 0x44;
        var rom = Filled(0x2000, 0x42);
        rom[0x1F34] = 0x84;
        var crt = BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.EpyxFastLoad,
            exromHigh: false,
            gameHigh: true,
            name: "EPYX FASTLOAD TEST",
            chips: [new Chip(0, 0x8000, rom)]);

        var result = c64.AttachCrtImage(crt, "epyx-fastload.crt");

        Assert.Equal((ushort)C64CrtHardwareType.EpyxFastLoad, result.HardwareType);
        Assert.Equal(0x42, c64.Mem.Read(0x8000));

        c64.CartridgeSlot.Tick(C64EpyxFastLoadCartridge.RomEnableCycles);

        Assert.Equal(C64CartridgeLines.Released, c64.CartridgeSlot.Lines);
        Assert.Equal(0x44, c64.Mem.Read(0x8000));
        Assert.Equal(0x84, c64.Mem.Read(0xDF34));

        Assert.Equal(0, c64.Mem.Read(0xDE00));

        Assert.Equal(new C64CartridgeLines(GameHigh: true, ExromHigh: false), c64.CartridgeSlot.Lines);
        Assert.Equal(0x42, c64.Mem.Read(0x8000));
    }

    [Fact]
    public void Factory_Creates_ActionReplay_With_Banked_Rom_And_Exported_Ram()
    {
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.ActionReplay,
            exromHigh: false,
            gameHigh: true,
            name: "ACTION REPLAY TEST",
            chips: CreateBankChips(4)));

        var cartridge = Assert.IsType<C64ActionReplayCartridge>(
            C64CrtCartridgeFactory.Create(image));

        Assert.Equal("ACTION REPLAY TEST", cartridge.Name);
        Assert.Equal(new C64CartridgeLines(GameHigh: true, ExromHigh: false), cartridge.Lines);
        Assert.Equal((ushort)0, cartridge.CurrentBank);
        Assert.Equal(0x10, cartridge.ReadROML(0x8000));

        cartridge.WriteIO(0xDE00, 0x11);

        Assert.Equal((ushort)2, cartridge.CurrentBank);
        Assert.Equal(new C64CartridgeLines(GameHigh: false, ExromHigh: false), cartridge.Lines);
        Assert.Equal(0x12, cartridge.ReadROML(0x8000));
        Assert.Equal(0x12, cartridge.ReadROMH(0xA000));
        Assert.Equal(0x12, cartridge.ReadIO(0xDF34));

        cartridge.WriteIO(0xDE00, 0x21);
        cartridge.WriteROML(0x8123, 0x42);
        cartridge.WriteIO(0xDF34, 0x84);

        Assert.True(cartridge.IsRamExported);
        Assert.Equal(0x42, cartridge.ReadROML(0x8123));
        Assert.Equal(0x84, cartridge.ReadIO(0xDF34));
        Assert.Equal(0x84, cartridge.ReadRam(0x1F34));
    }

    [Theory]
    [InlineData("missing-bank")]
    [InlineData("duplicate-bank")]
    [InlineData("wrong-address")]
    [InlineData("wrong-size")]
    [InlineData("bank-too-high")]
    public void Factory_Rejects_Invalid_ActionReplay_Images(string invalidShape)
    {
        Chip[] chips = invalidShape switch
        {
            "missing-bank" => CreateBankChips(3),
            "duplicate-bank" =>
            [
                new Chip(0, 0x8000, Filled(0x2000, 0x10)),
                new Chip(1, 0x8000, Filled(0x2000, 0x11)),
                new Chip(2, 0x8000, Filled(0x2000, 0x12)),
                new Chip(2, 0x8000, Filled(0x2000, 0x13)),
            ],
            "wrong-address" =>
            [
                new Chip(0, 0x9000, Filled(0x2000, 0x10)),
                .. CreateBankChips(4)[1..],
            ],
            "wrong-size" =>
            [
                new Chip(0, 0x8000, Filled(0x1000, 0x10)),
                .. CreateBankChips(4)[1..],
            ],
            "bank-too-high" =>
            [
                .. CreateBankChips(3),
                new Chip(4, 0x8000, Filled(0x2000, 0x14)),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(invalidShape)),
        };
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.ActionReplay,
            exromHigh: false,
            gameHigh: true,
            chips: chips));

        Assert.Throws<C64CrtImageException>(
            () => C64CrtCartridgeFactory.Create(image));
    }

    [Fact]
    public void ActionReplay_Attach_Maps_Writable_Ram_And_Can_Disable()
    {
        var c64 = BuildC64();
        c64.RAM[0x8000] = 0x44;
        var crt = BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.ActionReplay,
            exromHigh: false,
            gameHigh: true,
            name: "ACTION REPLAY TEST",
            chips: CreateBankChips(4));

        var result = c64.AttachCrtImage(crt, "action-replay.crt");

        Assert.Equal((ushort)C64CrtHardwareType.ActionReplay, result.HardwareType);
        Assert.Equal(0x10, c64.Mem.Read(0x8000));

        c64.Mem.Write(0xDE00, 0x20);
        c64.Mem.Write(0x8123, 0x42);
        c64.Mem.Write(0xDF34, 0x84);

        Assert.Equal(0x42, c64.Mem.Read(0x8123));
        Assert.Equal(0x84, c64.Mem.Read(0xDF34));

        c64.Mem.Write(0xDE00, 0x04);

        Assert.Equal(C64CartridgeLines.Released, c64.CartridgeSlot.Lines);
        Assert.Equal(0x44, c64.Mem.Read(0x8000));
    }

    [Fact]
    public void ActionReplay_Freeze_Enters_Ultimax_And_Services_Nmi_From_Cartridge()
    {
        var c64 = BuildC64();
        var banks = CreateBankChips(4);
        banks[0].Data[0x1FFA] = 0x34;
        banks[0].Data[0x1FFB] = 0x12;
        var crt = BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.ActionReplay,
            exromHigh: false,
            gameHigh: true,
            chips: banks);
        c64.AttachCrtImage(crt, "action-replay.crt");
        c64.Mem.Write(0xDE00, 0x1C); // Select bank 3 and disable, like exiting to BASIC.

        var frozen = c64.FreezeAttachedCartridge();

        var cartridge = Assert.IsType<C64ActionReplayCartridge>(
            c64.CartridgeSlot.AttachedCartridge);
        Assert.True(frozen);
        Assert.True(cartridge.IsFreezeMode);
        Assert.True(cartridge.IsRamExported);
        Assert.Equal((ushort)0, cartridge.CurrentBank);
        Assert.Equal(new C64CartridgeLines(GameHigh: false, ExromHigh: true), cartridge.Lines);
        Assert.Equal((ushort)0x1234, c64.CPU.PC);
    }

    [Fact]
    public void Factory_Creates_FinalCartridgeIII_With_Banked_Rom_And_IO_Mirrors()
    {
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            name: "FINAL CARTRIDGE III TEST",
            chips: CreateFinalCartridgeIIIBankChips(4)));

        var cartridge = Assert.IsType<C64FinalCartridgeIIICartridge>(
            C64CrtCartridgeFactory.Create(image));

        Assert.Equal("FINAL CARTRIDGE III TEST", cartridge.Name);
        Assert.Equal(new C64CartridgeLines(GameHigh: false, ExromHigh: false), cartridge.Lines);
        Assert.Equal((ushort)0, cartridge.CurrentBank);
        Assert.Equal(0x10, cartridge.ReadROML(0x8000));
        Assert.Equal(0x20, cartridge.ReadROMH(0xA000));
        Assert.Equal(0x10, cartridge.ReadIO(0xDE34));
        Assert.Equal(0x10, cartridge.ReadIO(0xDF34));

        cartridge.WriteIO(0xDFFF, 0x62); // Bank 2, 8K mode, NMI released.

        Assert.Equal((ushort)2, cartridge.CurrentBank);
        Assert.Equal(new C64CartridgeLines(GameHigh: true, ExromHigh: false), cartridge.Lines);
        Assert.Equal(0x12, cartridge.ReadROML(0x8000));
        Assert.False(cartridge.HasROMH);
        Assert.Equal(0x12, cartridge.ReadIO(0xDE34));
        Assert.Equal(0x12, cartridge.ReadIO(0xDF34));
    }

    [Fact]
    public void Factory_Creates_Sixteen_Bank_FinalCartridgeIIIPlus()
    {
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: CreateFinalCartridgeIIIBankChips(16)));
        var cartridge = Assert.IsType<C64FinalCartridgeIIICartridge>(
            C64CrtCartridgeFactory.Create(image));

        cartridge.WriteIO(0xDFFF, 0x4F); // Bank 15, 16K mode, NMI released.

        Assert.Equal((ushort)15, cartridge.CurrentBank);
        Assert.Equal(0x1F, cartridge.ReadROML(0x8000));
        Assert.Equal(0x2F, cartridge.ReadROMH(0xA000));
    }

    [Theory]
    [InlineData("wrong-count")]
    [InlineData("duplicate-bank")]
    [InlineData("wrong-address")]
    [InlineData("wrong-size")]
    [InlineData("bank-too-high")]
    public void Factory_Rejects_Invalid_FinalCartridgeIII_Images(string invalidShape)
    {
        Chip[] chips = invalidShape switch
        {
            "wrong-count" => CreateFinalCartridgeIIIBankChips(3),
            "duplicate-bank" =>
            [
                .. CreateFinalCartridgeIIIBankChips(3),
                CreateFinalCartridgeIIIBankChips(4)[2],
            ],
            "wrong-address" =>
            [
                new Chip(0, 0xA000, Filled(0x4000, 0x10)),
                .. CreateFinalCartridgeIIIBankChips(4)[1..],
            ],
            "wrong-size" =>
            [
                new Chip(0, 0x8000, Filled(0x2000, 0x10)),
                .. CreateFinalCartridgeIIIBankChips(4)[1..],
            ],
            "bank-too-high" =>
            [
                .. CreateFinalCartridgeIIIBankChips(3),
                new Chip(4, 0x8000, Filled(0x4000, 0x14)),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(invalidShape)),
        };
        var image = C64CrtParser.Parse(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: chips));

        Assert.Throws<C64CrtImageException>(
            () => C64CrtCartridgeFactory.Create(image));
    }

    [Fact]
    public void FinalCartridgeIII_Hidden_Register_Ignores_Writes_Until_Freeze()
    {
        var cartridge = Assert.IsType<C64FinalCartridgeIIICartridge>(
            C64CrtCartridgeFactory.Create(C64CrtParser.Parse(BuildCrt(
                hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
                exromHigh: false,
                gameHigh: false,
                chips: CreateFinalCartridgeIIIBankChips(4)))));

        cartridge.WriteIO(0xDFFF, 0xF3); // Bank 3, released mode, hide register.
        cartridge.WriteIO(0xDFFF, 0x40);

        Assert.False(cartridge.IsRegisterEnabled);
        Assert.Equal((ushort)3, cartridge.CurrentBank);
        Assert.Equal(C64CartridgeLines.Released, cartridge.Lines);

        cartridge.Freeze();
        cartridge.WriteIO(0xDFFF, 0x40);

        Assert.True(cartridge.IsRegisterEnabled);
        Assert.False(cartridge.IsFreezeMode);
        Assert.Equal((ushort)0, cartridge.CurrentBank);
        Assert.Equal(new C64CartridgeLines(GameHigh: false, ExromHigh: false), cartridge.Lines);
    }

    [Fact]
    public void FinalCartridgeIII_Control_Register_Drives_The_Cartridge_Nmi_Line()
    {
        var c64 = BuildC64();
        var chips = CreateFinalCartridgeIIIBankChips(4);
        chips[1].Data[0x3FFA] = 0x78;
        chips[1].Data[0x3FFB] = 0x56;
        c64.AttachCrtImage(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: chips));

        c64.Mem.Write(0xDFFF, 0x11); // Bank 1, Ultimax, assert NMI.

        Assert.True(c64.CartridgeSlot.NmiLineActive);
        Assert.True(c64.CPU.CPUInterrupts.NMIPending);
        c64.CPU.ProcessPendingInterrupts(c64.Mem);
        Assert.Equal((ushort)0x5678, c64.CPU.PC);

        c64.Mem.Write(0xDFFF, 0x51); // Keep mapping and bank, release NMI.

        Assert.False(c64.CartridgeSlot.NmiLineActive);
    }

    [Fact]
    public void FinalCartridgeIII_Freeze_Keeps_Selected_Bank_And_Services_Its_Nmi_Vector()
    {
        var c64 = BuildC64();
        var chips = CreateFinalCartridgeIIIBankChips(4);
        chips[2].Data[0x1FE0] = 0x8E; // IO2 mirror at $DFE0 should come from the selected bank.
        chips[2].Data[0x3FFA] = 0x34;
        chips[2].Data[0x3FFB] = 0x12;
        c64.AttachCrtImage(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: chips));
        c64.Mem.Write(0xDFFF, 0x72); // Bank 2, released mode.

        var frozen = c64.FreezeAttachedCartridge();

        var cartridge = Assert.IsType<C64FinalCartridgeIIICartridge>(
            c64.CartridgeSlot.AttachedCartridge);
        Assert.True(frozen);
        Assert.True(cartridge.IsFreezeMode);
        Assert.True(cartridge.IsRegisterEnabled);
        Assert.Equal((ushort)2, cartridge.CurrentBank);
        Assert.Equal(new C64CartridgeLines(GameHigh: false, ExromHigh: true), cartridge.Lines);
        Assert.Equal(0x8E, c64.Mem.Read(0xDFE0));
        Assert.Equal((ushort)0x1234, c64.CPU.PC);
    }

    [Fact]
    public void FinalCartridgeIII_Freeze_Writes_To_Ram_Under_ROMH_For_VIC_Bank_Three()
    {
        var c64 = BuildC64();
        var chips = CreateFinalCartridgeIIIBankChips(4);
        chips[3].Data[0x3FFA] = 0x34;
        chips[3].Data[0x3FFB] = 0x12;
        c64.AttachCrtImage(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: chips));
        c64.Mem.Write(0xDFFF, 0x73); // Bank 3, released mode.
        c64.FreezeAttachedCartridge();
        c64.Vic2.SetVIC2Bank(0x00); // VIC bank 3: C64 $C000-$FFFF.

        c64.Mem.Write(0xE123, 0x5A);

        Assert.Equal(0x5A, c64.RAM[0xE123]);
        Assert.Equal(0x5A, c64.Vic2.Vic2Mem[0x2123]);
        Assert.NotEqual(0x5A, c64.Mem.Read(0xE123)); // CPU still reads ROMH while RAM is hidden behind it.
    }

    [Fact]
    public void FinalCartridgeIII_Ultimax_Exposes_ROMH_To_VIC()
    {
        var c64 = BuildC64();
        var chips = CreateFinalCartridgeIIIBankChips(4);
        chips[3].Data[0x2000 + 0x1140] = 0xA5; // ROMH byte visible to VIC bank 3 at C64 $F140.
        c64.RAM[0xF140] = 0x00;
        c64.AttachCrtImage(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: chips));

        c64.Mem.Write(0xDFFF, 0x13); // Bank 3, Ultimax, assert cartridge NMI line.
        c64.Vic2.SetVIC2Bank(0x00); // VIC bank 3: C64 $C000-$FFFF.

        Assert.Equal(23, c64.CurrentBank);
        Assert.Equal(0xA5, c64.Vic2.ReadMemory(0x3140));
        Assert.Equal(0x00, c64.RAM[0xF140]);
    }

    [Fact]
    public void FinalCartridgeIII_Ultimax_Sprite_Data_Can_Come_From_ROMH()
    {
        var c64 = BuildC64();
        var chips = CreateFinalCartridgeIIIBankChips(4);
        chips[3].Data[0x2000 + 0x1140] = 0x80; // Sprite pointer $C5 starts at VIC $3140 / C64 $F140.
        c64.RAM[0xC7FA] = 0xC5; // Sprite 2 pointer in VIC bank 3, default screen matrix $0400.
        c64.RAM[0xF140] = 0x00;
        c64.AttachCrtImage(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: chips));
        c64.RAM[0xC7FA] = 0xC5;
        c64.RAM[0xF140] = 0x00;
        c64.Vic2.SetVIC2Bank(0x00); // VIC bank 3: C64 $C000-$FFFF.
        c64.Vic2.MemorySetupStore(0xD018, 0x10); // Screen matrix $0400; sprite pointers at $07F8.
        c64.Mem.Write(0xDFFF, 0x73); // Bank 3, released mode: sprite data comes from underlying RAM.

        Assert.Equal(0x00, c64.Vic2.SpriteManager.Sprites[2].Data.Rows[0].Bytes[0]);

        c64.Mem.Write(0xDFFF, 0x13); // Bank 3, Ultimax, assert cartridge NMI line.

        var spriteData = c64.Vic2.SpriteManager.Sprites[2].Data;

        Assert.Equal(0x80, spriteData.Rows[0].Bytes[0]);
        Assert.Equal(0x00, c64.RAM[0xF140]);
    }

    [Fact]
    public void FinalCartridgeIII_Freeze_Holds_Nmi_Line_Until_Control_Register_Releases_It()
    {
        var c64 = BuildC64();
        var chips = CreateFinalCartridgeIIIBankChips(4);
        chips[0].Data[0x3FFA] = 0x34;
        chips[0].Data[0x3FFB] = 0x12;
        c64.AttachCrtImage(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: chips));

        var frozen = c64.FreezeAttachedCartridge();

        var cartridge = Assert.IsType<C64FinalCartridgeIIICartridge>(
            c64.CartridgeSlot.AttachedCartridge);
        Assert.True(frozen);
        Assert.True(cartridge.NmiLineActive);
        Assert.True(c64.CPU.CPUInterrupts.IsNMISourceActive("CartridgeNmi"));
        Assert.False(c64.CPU.NMI);
        Assert.Equal((ushort)0x1234, c64.CPU.PC);

        c64.Mem.Write(0xDFFF, 0x40);

        Assert.False(cartridge.NmiLineActive);
        Assert.False(c64.CPU.CPUInterrupts.IsNMISourceActive("CartridgeNmi"));
    }

    [Fact]
    public void FinalCartridgeIII_Control_Register_Releases_Nmi_Line()
    {
        var c64 = BuildC64();
        var chips = CreateFinalCartridgeIIIBankChips(4);
        c64.AttachCrtImage(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: chips));

        c64.Mem.Write(0xDFFF, 0x00);

        var cartridge = Assert.IsType<C64FinalCartridgeIIICartridge>(
            c64.CartridgeSlot.AttachedCartridge);
        Assert.True(cartridge.NmiLineActive);
        Assert.True(c64.CPU.CPUInterrupts.IsNMISourceActive("CartridgeNmi"));

        c64.Mem.Write(0xDFFF, 0x40);

        Assert.False(cartridge.NmiLineActive);
        Assert.False(c64.CPU.CPUInterrupts.IsNMISourceActive("CartridgeNmi"));
    }

    [Fact]
    public void FinalCartridgeIII_Control_Register_Does_Not_Retrigger_Nmi_While_Line_Is_Active()
    {
        var c64 = BuildC64();
        var chips = CreateFinalCartridgeIIIBankChips(4);
        c64.AttachCrtImage(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: chips));
        c64.FreezeAttachedCartridge();
        c64.CPU.ProcessPendingInterrupts(c64.Mem);

        Assert.True(c64.CartridgeSlot.NmiLineActive);
        Assert.False(c64.CPU.NMI);

        c64.Mem.Write(0xDFFF, 0x03);

        var cartridge = Assert.IsType<C64FinalCartridgeIIICartridge>(
            c64.CartridgeSlot.AttachedCartridge);
        Assert.True(cartridge.NmiLineActive);
        Assert.False(c64.CPU.NMI);
        Assert.True(c64.CPU.CPUInterrupts.IsNMISourceActive("CartridgeNmi"));
    }

    [Fact]
    public void FinalCartridgeIII_Repeated_Active_Nmi_Register_Write_Does_Not_Retrigger_Nmi()
    {
        var c64 = BuildC64();
        var chips = CreateFinalCartridgeIIIBankChips(4);
        c64.AttachCrtImage(BuildCrt(
            hardwareType: (ushort)C64CrtHardwareType.FinalCartridgeIII,
            exromHigh: false,
            gameHigh: false,
            chips: chips));

        c64.Mem.Write(0xDFFF, 0x03);
        c64.CPU.ProcessPendingInterrupts(c64.Mem);

        Assert.False(c64.CPU.NMI);

        c64.Mem.Write(0xDFFF, 0x03);

        Assert.False(c64.CPU.NMI);
        Assert.True(c64.CPU.CPUInterrupts.IsNMISourceActive("CartridgeNmi"));
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

    private static Chip[] CreateFinalCartridgeIIIBankChips(int count)
        => Enumerable.Range(0, count)
            .Select(bank => new Chip(
                (ushort)bank,
                0x8000,
                [
                    .. Filled(0x2000, (byte)(0x10 + bank)),
                    .. Filled(0x2000, (byte)(0x20 + bank)),
                ]))
            .ToArray();

    private sealed record Chip(ushort Bank, ushort Address, byte[] Data);
}
