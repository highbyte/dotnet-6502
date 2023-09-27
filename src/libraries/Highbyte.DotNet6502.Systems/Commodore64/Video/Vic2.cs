using System.Net;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2Sprite;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// --- CHARACTER GENERATOR ROM ---
/// From the CPU perspective, the character generator ROM (chargen) lives at addresses 0xd000 - 0xdfff.
/// But only if that is enabled by C64 IO port bank layout at address 0x0001.
/// By default, the CPU sees IO control addresses in that range.
/// 
/// The VIC 2 sees 16K of memory space, with different parts of CPU memory and ROM mapped at different locations.
///
/// </summary>
public class Vic2
{
    public C64? C64 { get; private set; }
    public Vic2ModelBase? Vic2Model { get; private set; }
    public Vic2Screen? Vic2Screen { get; private set; }
    /// <summary>
    /// Vic2 screem memory for text, graphics and sprites.
    /// </summary>
    public Memory? Vic2Mem { get; private set; }

    /// <summary>
    /// Vic2 IO registers and color mem storage.
    /// </summary>
    public Memory? Vic2IOStorage { get; private set; }

    public Vic2IRQ? Vic2IRQ { get; private set; }

    public const int CHARACTERSET_NUMBER_OF_CHARCTERS = 256;
    public const int CHARACTERSET_ONE_CHARACTER_BYTES = 8;      // 8 bytes (one line per byte) for each character.
    public const int CHARACTERSET_SIZE = CHARACTERSET_NUMBER_OF_CHARCTERS * CHARACTERSET_ONE_CHARACTER_BYTES;    // = 1024 (0x0400) bytes. 256 characters, where each character takes up 8 bytes (1 byte per character line)

    public const int SPRITE_POINTERS_START_ADDRESS = 0x07f8;    // Range 0x07f8 - 0x07ff are offset from start of VIC2 screen memory (which can be relocated) to sprite pointers 0-7

    public ulong CyclesConsumedCurrentVblank { get; private set; } = 0;

    public byte CurrentVIC2Bank { get; private set; }
    private ushort _currentVIC2BankOffset = 0;

    // Offset into the currently selected VIC2 bank (Mem.SetMemoryConfiguration(bank))
    public ushort CharacterSetAddressInVIC2Bank => _currentVIC2BankOffset;
    // True if CharacterSetAddressInVIC2Bank points to location where Chargen ROM (two charsets, unshifted & shifted) is "shadowed".
    public bool CharacterSetAddressInVIC2BankIsChargenROMShifted => _currentVIC2BankOffset == 0x1000;
    public bool CharacterSetAddressInVIC2BankIsChargenROMUnshifted => _currentVIC2BankOffset == 0x1800;


    private ushort _currentRasterLineInternal = ushort.MaxValue;
    public ushort CurrentRasterLine => _currentRasterLineInternal;

    public bool Is38ColumnDisplayEnabled => !ReadIOStorage(Vic2Addr.SCROLL_X).IsBitSet(3);
    public byte FineScrollXValue => (byte)(ReadIOStorage(Vic2Addr.SCROLL_X) & 0b0000_0111);    // Value 0-7
    public bool Is24RowDisplayEnabled => !ReadIOStorage(Vic2Addr.SCREEN_CONTROL_REGISTER_1).IsBitSet(3);
    public byte FineScrollYValue => (byte)(ReadIOStorage(Vic2Addr.SCREEN_CONTROL_REGISTER_1) & 0b0000_0111);    // Value 0-7

    public event EventHandler<CharsetAddressChangedEventArgs> CharsetAddressChanged;
    protected virtual void OnCharsetAddressChanged(CharsetAddressChangedEventArgs e)
    {
        var handler = CharsetAddressChanged;
        handler?.Invoke(this, e);
    }

    public Dictionary<int, byte>? ScreenLineBorderColor { get; private set; }
    public Dictionary<int, byte>? ScreenLineBackgroundColor { get; private set; }
    public Vic2ScreenLayouts? ScreenLayouts { get; private set; }
    public Vic2SpriteManager? SpriteManager { get; private set; }

    private Vic2() { }

    public static Vic2 BuildVic2(Vic2ModelBase vic2Model, C64 c64)
    {
        var vic2Mem = CreateVic2Memory(c64);
        var vic2IOStorage = CreateVic2IOStorage(c64);
        var vic2IRQ = new Vic2IRQ();

        var screenLineBorderColorLookup = InitializeScreenLineBorderColorLookup(vic2Model);
        var screenLineBackgroundColorLookup = InitializeScreenLineBackgroundColorLookup(vic2Model);

        var vic2 = new Vic2()
        {
            C64 = c64,
            Vic2Mem = vic2Mem,
            Vic2IOStorage = vic2IOStorage,
            Vic2Model = vic2Model,
            Vic2IRQ = vic2IRQ,
            ScreenLineBorderColor = screenLineBorderColorLookup,
            ScreenLineBackgroundColor = screenLineBackgroundColorLookup,
        };

        var vic2Screen = new Vic2Screen(vic2Model, c64.CpuFrequencyHz);
        vic2.Vic2Screen = vic2Screen;

        var vic2ScreenLayouts = new Vic2ScreenLayouts(vic2);
        vic2.ScreenLayouts = vic2ScreenLayouts;

        var spriteManager = new Vic2SpriteManager(vic2);
        vic2.SpriteManager = spriteManager;

        return vic2;
    }

    private static Dictionary<int, byte> InitializeScreenLineBorderColorLookup(Vic2ModelBase vic2Model)
    {
        var screenLineBorderColor = new Dictionary<int, byte>();
        for (ushort i = 0; i < vic2Model.TotalHeight; i++)
        {
            screenLineBorderColor.Add(i, 0);
        }
        return screenLineBorderColor;
    }

    private static Dictionary<int, byte> InitializeScreenLineBackgroundColorLookup(Vic2ModelBase vic2Model)
    {
        var screenLineBackgroundColor = new Dictionary<int, byte>();
        for (ushort i = 0; i < vic2Model.TotalHeight; i++)
        {
            if (!vic2Model.IsRasterLineInMainScreen(i))
                continue;
            screenLineBackgroundColor.Add(i, 0);
        }
        return screenLineBackgroundColor;
    }

    public void MapIOLocations(Memory c64Mem)
    {
        // Address 0xd011: "Screen Control Register 1"
        c64Mem.MapReader(Vic2Addr.SCREEN_CONTROL_REGISTER_1, ScrCtrlReg1Load);
        c64Mem.MapWriter(Vic2Addr.SCREEN_CONTROL_REGISTER_1, ScrCtrlReg1Store);

        // Address 0xd012: "Current Raster Line"
        c64Mem.MapReader(Vic2Addr.CURRENT_RASTER_LINE, RasterLoad);
        c64Mem.MapWriter(Vic2Addr.CURRENT_RASTER_LINE, RasterStore);

        // Address 0xd016: "Horizontal Fine Scrolling and Control Register"
        c64Mem.MapReader(Vic2Addr.SCROLL_X, ScrollXLoad);
        c64Mem.MapWriter(Vic2Addr.SCROLL_X, ScrollXStore);

        // Address 0xd018: "Memory setup" (VIC2 pointer for charset/bitmap & screen memory)
        c64Mem.MapReader(Vic2Addr.MEMORY_SETUP, MemorySetupLoad);
        c64Mem.MapWriter(Vic2Addr.MEMORY_SETUP, MemorySetupStore);

        // Address 0xd019: "VIC Interrupt Flag Register"
        c64Mem.MapReader(Vic2Addr.VIC_IRQ, VICIRQLoad);
        c64Mem.MapWriter(Vic2Addr.VIC_IRQ, VICIRQStore);

        // Address 0xd01a: "IRQ Mask Register"
        c64Mem.MapReader(Vic2Addr.IRQ_MASK, IRQMASKLoad);
        c64Mem.MapWriter(Vic2Addr.IRQ_MASK, IRQMASKStore);

        // Address 0xd01c: "Sprite multi-color enable"
        c64Mem.MapReader(Vic2Addr.SPRITE_MULTICOLOR_ENABLE, SpriteMultiColorEnableLoad);
        c64Mem.MapWriter(Vic2Addr.SPRITE_MULTICOLOR_ENABLE, SpriteMultiColorEnableStore);

        // Address 0xd01e: "Sprite-to-sprite collision"
        c64Mem.MapReader(Vic2Addr.SPRITE_TO_SPRITE_COLLISION, SpriteToSpriteCollisionLoad);
        c64Mem.MapWriter(Vic2Addr.SPRITE_TO_SPRITE_COLLISION, SpriteToSpriteCollisionStore);

        // Address 0xd01f: "Sprite-to-background collision"
        c64Mem.MapReader(Vic2Addr.SPRITE_TO_BACKGROUND_COLLISION, SpriteToBackgroundCollisionLoad);
        c64Mem.MapWriter(Vic2Addr.SPRITE_TO_BACKGROUND_COLLISION, SpriteToBackgroundCollisionStore);

        // Address 0xd020: Border color
        c64Mem.MapReader(Vic2Addr.BORDER_COLOR, BorderColorLoad);
        c64Mem.MapWriter(Vic2Addr.BORDER_COLOR, BorderColorStore);
        // Address 0xd021: Background color
        c64Mem.MapReader(Vic2Addr.BACKGROUND_COLOR, BackgroundColorLoad);
        c64Mem.MapWriter(Vic2Addr.BACKGROUND_COLOR, BackgroundColorStore);

        // Address 0xd025: Sprite multi-color 0
        c64Mem.MapReader(Vic2Addr.SPRITE_MULTI_COLOR_0, SpriteMultiColor0Load);
        c64Mem.MapWriter(Vic2Addr.SPRITE_MULTI_COLOR_0, SpriteMultiColor0Store);
        // Address 0xd026: Sprite multi-color 1
        c64Mem.MapReader(Vic2Addr.SPRITE_MULTI_COLOR_1, SpriteMultiColor1Load);
        c64Mem.MapWriter(Vic2Addr.SPRITE_MULTI_COLOR_1, SpriteMultiColor1Store);

        // Addresses 0xd027 - 0xd02e: Sprite colors
        for (ushort address = Vic2Addr.SPRITE_0_COLOR; address <= Vic2Addr.SPRITE_7_COLOR; address++)
        {
            c64Mem.MapReader(address, SpriteColorLoad);
            c64Mem.MapWriter(address, SpriteColorStore);
        }

        // Address 0xdd00: "Port A" (VIC2 bank & serial bus)
        c64Mem.MapReader(Vic2Addr.PORT_A, PortALoad);
        c64Mem.MapWriter(Vic2Addr.PORT_A, PortAStore);
    }

    /// <summary>
    /// Method to be called before each write to memory by the CPU.
    /// It's used for optimization to detect changes in VIC2 video memory.
    /// </summary>
    /// <param name="c64Address"></param>
    /// <param name="value"></param>
    public void InspectVic2MemoryValueUpdateFromCPU(ushort c64Address, byte value)
    {
        var vic2Address = GetVic2FromC64Address(c64Address);
        if (vic2Address.HasValue)
        {
            SpriteManager.DetectChangesToSpriteData(vic2Address.Value, value);
        }
    }

    /// <summary>
    /// Converts a address seen by the CPU (64K) to a VIC2 video memory address (16K).
    /// If address is not mapped by the VIC2 it returns null.
    /// </summary>
    /// <param name="c64Address"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private ushort? GetVic2FromC64Address(ushort c64Address)
    {
        ushort? vic2Address;

        switch (CurrentVIC2Bank)
        {
            case 0:
                vic2Address = c64Address switch
                {
                    >= 0x0000 and < 0x0fff => c64Address,   // video ram
                    >= 0x1000 and < 0x1fff => c64Address,   // chargen ROM
                    >= 0x2000 and < 0x3fff => c64Address,   // video ram
                    _ => null,  // not a address mapped by VIC2
                };
                break;

            case 1:
                vic2Address = c64Address switch
                {
                    >= 0x4000 and < 0x7fff => (ushort)(c64Address - 0x4000),   // video ram
                    _ => null,  // not a address mapped by VIC2
                };
                break;

            case 2:
                vic2Address = c64Address switch
                {
                    >= 0x8000 and < 0x8fff => (ushort)(c64Address - 0x8000),   // video ram
                    >= 0x9000 and < 0x9fff => (ushort)(c64Address - 0x8000),   // chargen rom
                    >= 0xa000 and < 0xbfff => (ushort)(c64Address - 0x8000),   // video ram
                    _ => null,  // not a address mapped by VIC2
                };
                break;

            case 3:
                vic2Address = c64Address switch
                {
                    >= 0xc000 and < 0xffff => (ushort)(c64Address - 0xc000),   // video ram
                    _ => null,  // not a address mapped by VIC2
                };
                break;

            default:
                throw new NotImplementedException($"VIC2 bank {CurrentVIC2Bank} not implemented yet");
        }
        return vic2Address;
    }

    /// <summary>
    /// Map VIC2 16K addressable memory to different parts of the C64 64K RAM and chargen ROM
    /// </summary>
    /// <param name="ram"></param>
    /// <param name="roms"></param>
    /// <returns></returns>
    private static Memory CreateVic2Memory(C64 c64)
    {
        var ram = c64.RAM;
        var romData = c64.ROMData;
        var chargen = romData[C64Config.CHARGEN_ROM_NAME];

        // Vic2 can use 4 different banks of 16KB of memory each. They map into C64 RAM or Chargen ROM depending on bank.
        var vic2Mem = new Memory(memorySize: 16 * 1024, numberOfConfigurations: 4, mapToDefaultRAM: false);

        // Map C64 RAM locations and ROM images to Vic2 memory banks.
        // Note: As the C64 RAM is sent as a array (which is a reference type in .NET), any change to the C64 RAM array will be reflected in the mapped VIC2 memory location (and vice versa).

        vic2Mem.SetMemoryConfiguration(0);
        vic2Mem.MapRAM(0x0000, ram, 0, 0x1000);
        vic2Mem.MapRAM(0x1000, chargen);    // Chargen ROM "shadow" appear here.  Assume chargen is 0x1000 (4096) bytes length.
        vic2Mem.MapRAM(0x2000, ram, 0x2000, 0x2000);

        vic2Mem.SetMemoryConfiguration(1);
        vic2Mem.MapRAM(0x0000, ram, 0x4000, 0x4000);

        vic2Mem.SetMemoryConfiguration(2);
        vic2Mem.MapRAM(0x0000, ram, 0x8000, 0x1000);
        vic2Mem.MapRAM(0x1000, chargen);    // Chargen ROM "shadow" appear here. Assume chargen is 0x1000 (4096) bytes length.
        vic2Mem.MapRAM(0x2000, ram, 0xa000, 0x2000);

        vic2Mem.SetMemoryConfiguration(3);
        vic2Mem.MapRAM(0x0000, ram, 0xc000, 0x4000);

        // Default to bank 0
        vic2Mem.SetMemoryConfiguration(0);

        return vic2Mem;
    }

    private static Memory CreateVic2IOStorage(C64 c64)
    {
        var ram = c64.RAM;

        // Vic2 IO Storage is 4K and is always mapped to address 0xd000 in the C64 memory map.
        var vic2IOStorage = new Memory(memorySize: 4 * 1024, numberOfConfigurations: 1, mapToDefaultRAM: false);

        vic2IOStorage.SetMemoryConfiguration(0);
        vic2IOStorage.MapRAM(0x0000, ram, 0xd000, 0x1000);

        return vic2IOStorage;
    }

    /// <summary>
    /// Writes byte to VIC2 IO Storage, with address specified as location C64 memory map, and translated to VIC2 IO Storage address (-0xd000).
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    private void WriteIOStorage(ushort address, byte value)
    {
        Vic2IOStorage[(ushort)(address - 0xd000)] = value;
    }
    public byte ReadIOStorage(ushort address)
    {
        return Vic2IOStorage[(ushort)(address - 0xd000)];
    }


    public void SpriteMultiColorEnableStore(ushort address, byte value)
    {
        var originalValue = ReadIOStorage(address);
        WriteIOStorage(address, value);
        for (int spriteNumber = 0; spriteNumber < 8; spriteNumber++)
        {
            if (originalValue.IsBitSet(spriteNumber) != value.IsBitSet(spriteNumber))
                SpriteManager.Sprites[spriteNumber].HasChanged(Vic2SpriteChangeType.All);
        }
    }
    public byte SpriteMultiColorEnableLoad(ushort address)
    {
        return ReadIOStorage(address);
    }

    public void SpriteToSpriteCollisionStore(ushort address, byte value)
    {
        WriteIOStorage(address, value);
    }
    public byte SpriteToSpriteCollisionLoad(ushort address)
    {
        return SpriteManager.GetSpriteToSpriteCollision();
    }

    public void SpriteToBackgroundCollisionStore(ushort address, byte value)
    {
        WriteIOStorage(address, value);
    }
    public byte SpriteToBackgroundCollisionLoad(ushort address)
    {
        return SpriteManager.GetSpriteToBackgroundCollision();
    }

    public void BorderColorStore(ushort address, byte value)
    {
        WriteIOStorage(address, (byte)(value & 0b0000_1111));   // Only bits 0-3 are stored;
    }
    public byte BorderColorLoad(ushort address)
    {
        return (byte)(ReadIOStorage(address) | 0b1111_0000); // Bits 4-7 are unused and always 1
    }
    public void BackgroundColorStore(ushort address, byte value)
    {
        WriteIOStorage(address, (byte)(value & 0b0000_1111)); ; // Only bits 0-3 are stored
    }
    public byte BackgroundColorLoad(ushort address)
    {
        return (byte)(ReadIOStorage(address) | 0b1111_0000); // Bits 4-7 are unused and always 1
    }

    public void SpriteColorStore(ushort address, byte value)
    {
        WriteIOStorage(address, (byte)(value & 0b0000_1111));   // Only bits 0-3 are stored;
        var spriteNumber = (address - Vic2Addr.SPRITE_0_COLOR);
        SpriteManager.Sprites[spriteNumber].HasChanged(Vic2SpriteChangeType.Color);
    }
    public byte SpriteColorLoad(ushort address)
    {
        return (byte)(ReadIOStorage(address) | 0b1111_0000); // Bits 4-7 are unused and always 1
    }

    public void SpriteMultiColor0Store(ushort address, byte value)
    {
        WriteIOStorage(address, (byte)(value & 0b0000_1111));   // Only bits 0-3 are stored;
        SpriteManager.SetAllDirty();
    }
    public byte SpriteMultiColor0Load(ushort address)
    {
        return (byte)(ReadIOStorage(address) | 0b1111_0000); // Bits 4-7 are unused and always 1
    }
    public void SpriteMultiColor1Store(ushort address, byte value)
    {
        WriteIOStorage(address, (byte)(value & 0b0000_1111)); ; // Only bits 0-3 are stored
        SpriteManager.SetAllDirty();
    }
    public byte SpriteMultiColor1Load(ushort address)
    {
        return (byte)(ReadIOStorage(address) | 0b1111_0000); // Bits 4-7 are unused and always 1
    }

    public void ScrollXStore(ushort address, byte value)
    {
        WriteIOStorage(address, (byte)(value & 0b0011_1111)); // Only bits 0-5 are stored
        SpriteManager.SetAllDirty();
    }
    public byte ScrollXLoad(ushort address)
    {
        return (byte)(ReadIOStorage(address) | 0b1100_0000); // Bits 6-7 are unused and always 1
    }

    public void MemorySetupStore(ushort address, byte value)
    {
        WriteIOStorage(address, value);

        // From VIC 2 perspective, IO address 0xd018 (bits 1-3) controls where within a VIC 2 "Bank" the character set is read from,
        // By default, these bits are %010, which is 0x1000-0x17ff offset from Bank below. 
        // If Bank 0 or bank 2 is selected, this points to a shadow copy of the Chargen ROM
        // %000, 0: 0x0000-0x07FF, 0-2047.
        // %001, 1: 0x0800-0x0FFF, 2048-4095.
        // %010, 2: 0x1000-0x17FF, 4096-6143.
        // %011, 3: 0x1800-0x1FFF, 6144-8191.
        // %100, 4: 0x2000-0x27FF, 8192-10239.
        // %101, 5: 0x2800-0x2FFF, 10240-12287.
        // %110, 6: 0x3000-0x37FF, 12288-14335.
        // %111, 7: 0x3800-0x3FFF, 14336-16383.
        var oldVIC2BankOffset = _currentVIC2BankOffset;
        var newTextModeMemOffset = (value & 0b00001110) >> 1;
        _currentVIC2BankOffset = newTextModeMemOffset switch
        {
            0b000 => 0,
            0b001 => 0x0800,
            0b010 => 0x1000,    // Default.
            0b011 => 0x1800,
            0b100 => 0x2000,
            0b101 => 0x2800,
            0b110 => 0x3000,
            0b111 => 0x3800,
            _ => throw new NotImplementedException(),
        };
        if (_currentVIC2BankOffset != oldVIC2BankOffset)
            OnCharsetAddressChanged(new());
    }

    public byte MemorySetupLoad(ushort address)
    {
        return ReadIOStorage(address);
    }

    public void PortAStore(ushort address, byte value)
    {
        WriteIOStorage(address, value);

        // --- VIC 2 BANKS ---
        // Bits 0-1 of PortA (0xdd00) selects VIC2 bank (inverted order, the highest value 3 means bank 0, and value 0 means bank 3)
        // The VIC 2 chip can be access the C64 RAM in 4 different banks, with 16K visible in each bank.
        // The bits 0 and 1 in IO address 0xdd00 controls which bank.
        // In two of the banks, the VIC 2 sees the "shadowed" ROM character set at one location within the 16K.
        // The rest memory ranges sees the C64 RAM. Even though C64 "port store" bank switching has set IO area at 0xd000-
        // Default is Bank 0 (bit 0 and 1 set).
        // |------------|-----------------|---------------------|-----------------------
        // |Bank Number | 16K Area in RAM | 0xdd00 bits pattern | ROM chars available?
        // |------------|-----------------|---------------------|-----------------------
        // | 0          | 0x0000 - 0x3fff | xxxx xx11           | Yes, at 0x1000-0x1fff
        // |------------|-----------------|---------------------|-----------------------
        // | 1          | 0x4000 - 0x7fff | xxxx xx10           | No
        // |------------|-----------------|---------------------|-----------------------
        // | 2          | 0x8000 - 0xbfff | xxxx xx01           | Yes, at 0x9000–0x9fff
        // |------------|-----------------|---------------------|-----------------------
        // | 3          | 0xc000 - 0xffff | xxxx xx00           | No
        // |------------|-----------------|---------------------|-----------------------

        int oldVIC2Bank = CurrentVIC2Bank;
        int newBankValue = value & 0b00000011;
        CurrentVIC2Bank = newBankValue switch
        {
            0b11 => 0,
            0b10 => 1,
            0b01 => 2,
            0b00 => 3,
            _ => throw new NotImplementedException(),
        };
        // TODO: Make sure to switch current VIC2 bank via mem.SetMemoryConfiguration(x), and just updating the internal variable CurrentVIC2Bank?
        if (CurrentVIC2Bank != oldVIC2Bank)
            OnCharsetAddressChanged(new());
    }

    public byte PortALoad(ushort address)
    {
        return ReadIOStorage(address);
    }

    public void ScrCtrlReg1Store(ushort address, byte value)
    {
        WriteIOStorage(address, (byte)(value & 0b0111_1111));

        // If the VIC2 model is PAL, then allow configuring the 8th bit of the raster line IRQ.
        // Note: As the Kernal ROM initializes this 8th bit for both NTSC and PAL (same ROM for both), we need this workaround here.
        // TODO: Should an enum be used for VIC2 model base type (PAL or NTSC)?
        if (Vic2Model.MaxVisibleHeight > 256)
        {
            // When writing to this register (SCRCTRL1) the seventh bit is the highest (eigth) for the the raster line IRQ setting.
            ushort bit7HighestRasterLineBitIRQ = (ushort)(value & 0b1000_0000);
            bit7HighestRasterLineBitIRQ = (ushort)(bit7HighestRasterLineBitIRQ << 1);

            if (!Vic2IRQ.ConfiguredIRQRasterLine.HasValue)
                Vic2IRQ.ConfiguredIRQRasterLine = 0;
            Vic2IRQ.ConfiguredIRQRasterLine = (ushort?)(Vic2IRQ.ConfiguredIRQRasterLine & 0b1111_1111);
            Vic2IRQ.ConfiguredIRQRasterLine = (ushort?)(Vic2IRQ.ConfiguredIRQRasterLine | bit7HighestRasterLineBitIRQ);

        }

#if DEBUG
        if (Vic2IRQ.ConfiguredIRQRasterLine > Vic2Model.TotalHeight)
            throw new Exception($"Internal error. Setting unreachable scan line for IRQ: {Vic2IRQ.ConfiguredIRQRasterLine}. Incorrect ROM for Vic2 model: {Vic2Model.Name} ?");
#endif

    }

    public byte ScrCtrlReg1Load(ushort address)
    {
        // As the seventh bit in this register (SCRCTRL1), the highest (eigth) bit of the current raster line is returned.
        byte bit7HighestRasterLineBit = (byte)((_currentRasterLineInternal & 0b0000_0001_0000_0000) >> 1);
        return (byte)(ReadIOStorage(address) | bit7HighestRasterLineBit);
    }

    public void RasterStore(ushort address, byte value)
    {
        WriteIOStorage(address, value);

        ushort newIRQRasterLine = 0;
        if (Vic2IRQ.ConfiguredIRQRasterLine.HasValue)
        {
            // When writing to this register, the value is used to store the first 8 bits of the raster line IRQ setting.
            newIRQRasterLine = (ushort)(Vic2IRQ.ConfiguredIRQRasterLine & 0b0000_0001_0000_0000);
        }
        Vic2IRQ.ConfiguredIRQRasterLine = newIRQRasterLine |= value;
    }

    public byte RasterLoad(ushort _)
    {
        // When reding from this register, the lowest 8 bits of the current raster line is returned.
        return (byte)(_currentRasterLineInternal & 0xff);
    }

    public void VICIRQStore(ushort _, byte value)
    {
        // "Any" flag does not have a separate latch. Setting this bit means clearing all latches.
        if (value.IsBitSet((int)IRQSource.Any))
        {
            foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
            {
                // "Any" flag, does not have a separate latch.
                if (source == IRQSource.Any)
                    continue;
                // Clear all individual latches.
                if (Vic2IRQ.IsTriggered(source, C64.CPU))
                    Vic2IRQ.ClearTrigger(source, C64.CPU);
            }
        }
        else
        {
            // Clear the individual latches that are specified.
            foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
            {
                // "Any" flag, does not have a separate latch.
                if (source == IRQSource.Any)
                    continue;
                // Clear individual latch.
                if (value.IsBitSet((int)source) && Vic2IRQ.IsTriggered(source, C64.CPU))
                    Vic2IRQ.ClearTrigger(source, C64.CPU);
            }
        }
    }

    public byte VICIRQLoad(ushort _)
    {
        byte value = 0b01110000;    // Bits 4-7 are unused and always set to 1.

        bool anyIRQSourceTriggered = false;
        // Set bit 0-3 based on which IRQ sources have been triggered
        foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
        {
            // "Any" flag does not have a separate trigger.
            if (source == IRQSource.Any)
                continue;
            if (Vic2IRQ.IsTriggered(source, C64.CPU))
            {
                value.SetBit((int)source);
                anyIRQSourceTriggered = true;
            }
        }
        // If any of the individual IRQ flags are set, also set the "Any" flag (bit 7)
        if (anyIRQSourceTriggered)
            value.SetBit((int)IRQSource.Any);
        else
            value.ClearBit((int)IRQSource.Any);

        return value;
    }

    public void IRQMASKStore(ushort _, byte value)
    {
        foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
        {
            if (source == IRQSource.Any)
                continue;
            if (value.IsBitSet((int)source))
                Vic2IRQ.Enable(source);
            else
                Vic2IRQ.Disable(source);
        }
    }
    public byte IRQMASKLoad(ushort _)
    {
        byte value = 0b11110000; // Bits 4-7 are unused and always set to 1.

        foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
        {
            if (source == IRQSource.Any)
                continue;
            if (Vic2IRQ.IsEnabled(source))
                value.SetBit((int)source);
        }
        return value;
    }

    public void AdvanceRaster(ulong cyclesConsumed)
    {
        var cpu = C64.CPU;
        var mem = C64.Mem;

        CyclesConsumedCurrentVblank += cyclesConsumed;

        // Raster line housekeeping.
        // Calculate the raster line based on how man CPU cycles has been executed this frame
        var newLine = (ushort)(CyclesConsumedCurrentVblank / Vic2Model.CyclesPerLine);
        if (newLine != _currentRasterLineInternal)
        {
#if DEBUG
            if (newLine > Vic2Model.TotalHeight)
                throw new Exception($"Internal error. Unreachable scan line: {newLine}. The CPU probably executed more cycles current frame than allowed.");
#endif
            _currentRasterLineInternal = newLine;

            // Process timers
            if (C64.TimerMode == TimerMode.UpdateEachRasterLine)
                C64.Cia.ProcessTimers(Vic2Model.CyclesPerLine);

            // Check if a IRQ should be issued for current raster line, and issue it.
            RaiseRasterIRQ(cpu);
        }

        // Remember colors for each raster line
        StoreBorderColorForRasterLine(_currentRasterLineInternal);
        StoreBackgroundColorForRasterLine(_currentRasterLineInternal);

        // Check if we have reached the end of the frame.
        if (CyclesConsumedCurrentVblank >= Vic2Model.CyclesPerFrame)
        {
            CyclesConsumedCurrentVblank = 0;
        }
    }

    private void RaiseRasterIRQ(CPU cpu)
    {
        // Check if a IRQ should be issued
        var source = IRQSource.RasterCompare;
        if ((_currentRasterLineInternal == Vic2IRQ.ConfiguredIRQRasterLine
            || (!Vic2IRQ.ConfiguredIRQRasterLine.HasValue & _currentRasterLineInternal >= Vic2Model.TotalHeight))
            && Vic2IRQ.IsEnabled(source)
            && !Vic2IRQ.IsTriggered(source, C64.CPU))
        {
            Vic2IRQ.Trigger(source, cpu);
        }
    }

    private void StoreBorderColorForRasterLine(ushort rasterLine)
    {
        var screenLine = Vic2Model.ConvertRasterLineToScreenLine(rasterLine);
        ScreenLineBorderColor[screenLine] = ReadIOStorage(Vic2Addr.BORDER_COLOR);
    }

    private void StoreBackgroundColorForRasterLine(ushort rasterLine)
    {
        if (!C64.Vic2.Vic2Model.IsRasterLineInMainScreen(rasterLine))
            return;

        var screenLine = Vic2Model.ConvertRasterLineToScreenLine(rasterLine);
        ScreenLineBackgroundColor[screenLine] = ReadIOStorage(Vic2Addr.BACKGROUND_COLOR);
    }

    /// <summary>
    /// Get 1 line (8 pixels, 1 byte) from current character for the character code at the specified column and row in screen memory
    /// </summary>
    /// <param name="characterCol"></param>
    /// <param name="characterRow"></param>
    /// <param name="line"></param>
    /// <returns></returns>
    public byte GetTextModeCharacterLine(int characterCol, int characterRow, int line)
    {
        var characterAddress = (ushort)(Vic2Addr.SCREEN_RAM_START + (characterRow * Vic2Screen.TextCols) + characterCol);
        var characterCode = Vic2Mem[characterAddress];
        var characterSetLineAddress = (ushort)(CharacterSetAddressInVIC2Bank + (characterCode * Vic2Screen.CharacterHeight) + line);
        return Vic2Mem[characterSetLineAddress];
    }
}
