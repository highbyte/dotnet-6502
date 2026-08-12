using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// The language card's $C080-$C08F semantics, exercised through the machine's address space rather
/// than against the card object directly — the point is what the CPU sees at $D000-$FFFF, and that
/// depends on the memory mapping as much as on the switch state.
/// </summary>
public class Apple2LanguageCardTests
{
    // A stand-in ROM whose bytes are recognisable and differ from anything written to the card.
    private const byte RomFillD000 = 0xAA;
    private const byte RomFillE000 = 0xBB;

    private static Apple2System BuildApple2WithRom(bool languageCardEnabled = true)
    {
        var rom = new byte[Apple2System.SystemRomSize];
        for (var offset = 0; offset < rom.Length; offset++)
        {
            var address = Apple2System.SystemRomStartAddress + offset;
            rom[offset] = address < Apple2System.UpperMemoryStartAddress ? RomFillD000 : RomFillE000;
        }

        var romData = new Dictionary<string, byte[]> { { Apple2SystemConfig.SYSTEM_ROM_NAME, rom } };
        return new Apple2System(
            new Apple2Config { LanguageCardEnabled = languageCardEnabled },
            NullLoggerFactory.Instance,
            romData);
    }

    /// <summary>Reads the switch, which is how software drives the card (a write works too).</summary>
    private static void Switch(Apple2System apple2, ushort address) => _ = apple2.Mem[address];

    /// <summary>Reads the switch twice, the sequence that unlocks writing to the card.</summary>
    private static void SwitchTwice(Apple2System apple2, ushort address)
    {
        Switch(apple2, address);
        Switch(apple2, address);
    }

    [Fact]
    public void At_Power_On_The_Rom_Is_Visible_And_The_Card_Is_Write_Protected()
    {
        var apple2 = BuildApple2WithRom();

        Assert.False(apple2.LanguageCard.ReadRam);
        Assert.False(apple2.LanguageCard.WriteEnabled);
        Assert.False(apple2.LanguageCard.Bank1Selected);   // bank 2 at power-on

        // A machine that never touches $C08x behaves exactly like one with no card fitted.
        Assert.Equal(RomFillD000, apple2.Mem[0xD000]);
        Assert.Equal(RomFillE000, apple2.Mem[0xE000]);

        apple2.Mem[0xD000] = 0x11;
        Assert.Equal(RomFillD000, apple2.Mem[0xD000]);
    }

    [Theory]
    // (addr & 3): 0 and 3 read the card, 1 and 2 read ROM. Bit 2 is not decoded, so $C084-$C087
    // mirror $C080-$C083.
    [InlineData(0xC080, true)]
    [InlineData(0xC081, false)]
    [InlineData(0xC082, false)]
    [InlineData(0xC083, true)]
    [InlineData(0xC084, true)]
    [InlineData(0xC085, false)]
    [InlineData(0xC086, false)]
    [InlineData(0xC087, true)]
    [InlineData(0xC088, true)]
    [InlineData(0xC08B, true)]
    [InlineData(0xC08A, false)]
    public void The_Switch_Address_Selects_Whether_Reads_Come_From_The_Card(int address, bool expectedReadRam)
    {
        var apple2 = BuildApple2WithRom();

        Switch(apple2, (ushort)address);

        Assert.Equal(expectedReadRam, apple2.LanguageCard.ReadRam);
    }

    [Theory]
    [InlineData(0xC080, false)]   // bit 3 clear: bank 2
    [InlineData(0xC083, false)]
    [InlineData(0xC088, true)]    // bit 3 set: bank 1
    [InlineData(0xC08B, true)]
    public void Bit_Three_Of_The_Switch_Address_Selects_The_Bank(int address, bool expectedBank1)
    {
        var apple2 = BuildApple2WithRom();

        Switch(apple2, (ushort)address);

        Assert.Equal(expectedBank1, apple2.LanguageCard.Bank1Selected);
    }

    [Fact]
    public void Writing_Needs_Two_Consecutive_Reads_Of_The_Switch()
    {
        var apple2 = BuildApple2WithRom();

        // One read arms the sequence but must not unlock the card: a single stray access — an
        // indexed read landing in $C08x — must not silently expose RAM under the ROM.
        Switch(apple2, 0xC083);
        Assert.True(apple2.LanguageCard.PreWrite);
        Assert.False(apple2.LanguageCard.WriteEnabled);

        apple2.Mem[0xD000] = 0x42;
        Assert.NotEqual((byte)0x42, apple2.Mem[0xD000]);

        // The second read completes it.
        Switch(apple2, 0xC083);
        Assert.True(apple2.LanguageCard.WriteEnabled);

        apple2.Mem[0xD000] = 0x42;
        Assert.Equal((byte)0x42, apple2.Mem[0xD000]);
    }

    /// <summary>
    /// Characterization of a KNOWN DEVIATION. Per Sather (UTAIIe:5-23, confirmed against
    /// two independent emulator implementations): WRITE enable is set only by an odd
    /// READ while PRE-WRITE is set — a WRITE resets PRE-WRITE without completing the
    /// sequence. The current implementation lets the write complete it. This is the
    /// substrate of the CPU-model bus-accuracy work:
    /// with correct card semantics plus real per-model bus sequences, an NMOS RMW on
    /// $C083 (read-write-write) must NOT unlock the card while a 65C02 RMW
    /// (read-read-write) must. When the card is fixed, this test's expectation flips.
    /// </summary>
    [Fact]
    public void Read_Then_Write_Of_The_Switch_Currently_Completes_The_Sequence_Known_Deviation()
    {
        var apple2 = BuildApple2WithRom();

        Switch(apple2, 0xC083);         // read: arms PRE-WRITE
        apple2.Mem[0xC083] = 0x00;      // write: real hardware resets PRE-WRITE, no unlock

        // Current behavior: the write completes the sequence (deviation).
        Assert.True(apple2.LanguageCard.WriteEnabled);
        Assert.False(apple2.LanguageCard.PreWrite);
    }

    [Fact]
    public void A_Write_To_The_Switch_Does_Not_Arm_The_Sequence()
    {
        var apple2 = BuildApple2WithRom();

        // Only reads set the pre-write flip-flop, so any number of writes leaves it protected.
        apple2.Mem[0xC083] = 0x00;
        apple2.Mem[0xC083] = 0x00;
        apple2.Mem[0xC083] = 0x00;

        Assert.False(apple2.LanguageCard.WriteEnabled);

        apple2.Mem[0xD000] = 0x42;
        Assert.NotEqual((byte)0x42, apple2.Mem[0xD000]);
    }

    [Fact]
    public void An_Even_Switch_Address_Write_Protects_The_Card_Again()
    {
        var apple2 = BuildApple2WithRom();
        SwitchTwice(apple2, 0xC083);
        Assert.True(apple2.LanguageCard.WriteEnabled);

        Switch(apple2, 0xC082);

        Assert.False(apple2.LanguageCard.WriteEnabled);
        Assert.False(apple2.LanguageCard.PreWrite);
    }

    [Fact]
    public void The_Two_D000_Banks_Hold_Different_Data()
    {
        var apple2 = BuildApple2WithRom();

        // Bank 1: read and write the card.
        SwitchTwice(apple2, 0xC08B);
        apple2.Mem[0xD000] = 0x11;
        Assert.Equal((byte)0x11, apple2.Mem[0xD000]);

        // Bank 2 at the same address is a different 4 KB.
        SwitchTwice(apple2, 0xC083);
        Assert.NotEqual((byte)0x11, apple2.Mem[0xD000]);
        apple2.Mem[0xD000] = 0x22;
        Assert.Equal((byte)0x22, apple2.Mem[0xD000]);

        // Switching back finds bank 1 as it was left.
        SwitchTwice(apple2, 0xC08B);
        Assert.Equal((byte)0x11, apple2.Mem[0xD000]);
    }

    [Fact]
    public void The_E000_Block_Is_Shared_By_Both_Banks()
    {
        var apple2 = BuildApple2WithRom();

        SwitchTwice(apple2, 0xC08B);   // bank 1, read+write card
        apple2.Mem[0xE000] = 0x33;
        Assert.Equal((byte)0x33, apple2.Mem[0xE000]);

        // $E000-$FFFF is a single 8 KB block, so the bank selection does not apply to it.
        SwitchTwice(apple2, 0xC083);   // bank 2
        Assert.Equal((byte)0x33, apple2.Mem[0xE000]);
    }

    [Fact]
    public void Reads_Can_Come_From_Rom_While_Writes_Go_To_The_Card()
    {
        var apple2 = BuildApple2WithRom();

        // $C081 twice: read ROM, write the card — the idiom for filling the card while still
        // executing from ROM.
        SwitchTwice(apple2, 0xC081);
        Assert.False(apple2.LanguageCard.ReadRam);
        Assert.True(apple2.LanguageCard.WriteEnabled);

        apple2.Mem[0xD000] = 0x55;
        Assert.Equal(RomFillD000, apple2.Mem[0xD000]);   // still reading ROM

        // Switch reads over to the card and the written byte is there.
        Switch(apple2, 0xC080);
        Assert.Equal((byte)0x55, apple2.Mem[0xD000]);
    }

    [Fact]
    public void Reset_Puts_Rom_Back_But_Keeps_The_Cards_Contents()
    {
        var apple2 = BuildApple2WithRom();
        SwitchTwice(apple2, 0xC083);
        apple2.Mem[0xD000] = 0x77;
        apple2.Mem[0xE000] = 0x88;

        apple2.Reset();

        // ROM has to be back before the CPU reads its reset vector.
        Assert.False(apple2.LanguageCard.ReadRam);
        Assert.False(apple2.LanguageCard.WriteEnabled);
        Assert.False(apple2.LanguageCard.Bank1Selected);
        Assert.Equal(RomFillD000, apple2.Mem[0xD000]);

        // A reset does not clear RAM: software parks code in the card across resets.
        SwitchTwice(apple2, 0xC083);
        Assert.Equal((byte)0x77, apple2.Mem[0xD000]);
        Assert.Equal((byte)0x88, apple2.Mem[0xE000]);
    }

    [Fact]
    public void Without_The_Card_The_Switches_Do_Nothing_And_Rom_Stays_Visible()
    {
        var apple2 = BuildApple2WithRom(languageCardEnabled: false);

        Assert.False(apple2.LanguageCardEnabled);

        // The same sequence that switches a fitted card in and unlocks writing.
        SwitchTwice(apple2, 0xC083);

        // $C080-$C08F reads as unconnected, exactly as an empty slot 0 would.
        Assert.Equal(Apple2SoftSwitches.UnconnectedReadValue, apple2.Mem[0xC083]);

        // ROM is still what the CPU sees, and writes still go nowhere.
        Assert.Equal(RomFillD000, apple2.Mem[0xD000]);
        Assert.Equal(RomFillE000, apple2.Mem[0xE000]);
        apple2.Mem[0xD000] = 0x42;
        Assert.Equal(RomFillD000, apple2.Mem[0xD000]);
    }

    [Fact]
    public void Without_The_Card_The_Machine_Keeps_Running_Across_A_Reset()
    {
        // Reset has to leave a 48 KB machine alone rather than trying to switch a memory
        // configuration that was never built.
        var apple2 = BuildApple2WithRom(languageCardEnabled: false);

        SwitchTwice(apple2, 0xC08B);
        apple2.Reset();

        Assert.Equal(RomFillD000, apple2.Mem[0xD000]);
        Assert.False(apple2.LanguageCard.ReadRam);
    }

    [Fact]
    public void The_Card_Adds_Sixteen_Kilobytes_Over_The_Machines_Forty_Eight()
    {
        var apple2 = BuildApple2WithRom();

        Assert.Equal(Apple2LanguageCard.RamSize, apple2.LanguageCard.Ram.Length);
        Assert.Equal(16 * 1024, apple2.LanguageCard.Ram.Length);

        // 48 KB of main RAM plus the card is the 64 KB ProDOS requires.
        Assert.Equal(64 * 1024, Apple2System.RamSize + apple2.LanguageCard.Ram.Length);
    }
}
