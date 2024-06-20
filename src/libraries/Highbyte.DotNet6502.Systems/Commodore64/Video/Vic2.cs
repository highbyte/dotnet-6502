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
    public C64 C64 { get; private set; } = default!;
    public Vic2ModelBase Vic2Model { get; private set; } = default!;
    public Vic2Screen Vic2Screen { get; private set; } = default!;
    /// <summary>
    /// Vic2 screem memory for text, graphics and sprites.
    /// </summary>
    public Memory Vic2Mem { get; private set; } = default!;

    public Vic2IRQ Vic2IRQ { get; private set; } = default!;

    public ulong CyclesConsumedCurrentVblank { get; private set; } = 0;

    public byte CurrentVIC2Bank { get; private set; }

    public ushort VideoMatrixBaseAddress { get; private set; }

    public DispMode DisplayMode
    {
        get
        {
            if (C64.ReadIOStorage(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER).IsBitSet(5))
                return DispMode.Bitmap;
            return DispMode.Text;
        }
    }
    public enum DispMode { Text = 0, Bitmap = 1 };

    public BitmMode BitmapMode
    {
        get
        {
            if (C64.ReadIOStorage(Vic2Addr.SCROLL_X_AND_SCREEN_CONTROL_REGISTER).IsBitSet(4))
                return BitmMode.MultiColor;
            return BitmMode.Standard;
        }
    }
    public enum BitmMode { Standard = 0, MultiColor = 1 };

    public CharMode CharacterMode
    {
        get
        {
            if (C64.ReadIOStorage(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER).IsBitSet(6))
                return CharMode.Extended;
            if (C64.ReadIOStorage(Vic2Addr.SCROLL_X_AND_SCREEN_CONTROL_REGISTER).IsBitSet(4))
                return CharMode.MultiColor;
            return CharMode.Standard;
        }
    }

    public enum CharMode { Standard = 0, Extended = 1, MultiColor = 2 };

    private ushort _currentRasterLineInternal = ushort.MaxValue;
    public ushort CurrentRasterLine => _currentRasterLineInternal;

    public bool Is38ColumnDisplayEnabled => !C64.ReadIOStorage(Vic2Addr.SCROLL_X_AND_SCREEN_CONTROL_REGISTER).IsBitSet(3);
    public byte FineScrollXValue => (byte)(C64.ReadIOStorage(Vic2Addr.SCROLL_X_AND_SCREEN_CONTROL_REGISTER) & 0b0000_0111);    // Value 0-7
    public int GetScrollX()
    {
        var scrollX = FineScrollXValue;
        // Note: In 38 column mode, the screen is shifted 1 pixel to the right (at least as it's shown in VICE emulator)
        if (Is38ColumnDisplayEnabled)
            scrollX += 1;
        return scrollX;
    }

    public bool Is24RowDisplayEnabled => !C64.ReadIOStorage(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER).IsBitSet(3);
    public byte FineScrollYValue => (byte)(C64.ReadIOStorage(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER) & 0b0000_0111);    // Value 0-7
    public int GetScrollY()
    {
        var scrollY = FineScrollYValue - 3; // Note: VIC2 Y scroll value is by default 3 (=no offset)
        // Note: In 24 row mode, the screen is shifted 1 pixel down (at least as it's shown in VICE emulator)
        if (Is24RowDisplayEnabled)
            scrollY += 1;
        return scrollY;
    }

    public class ScreenLineData : ICloneable
    {
        public byte BorderColor;
        public byte BackgroundColor0;
        public byte BackgroundColor1;
        public byte BackgroundColor2;
        public byte BackgroundColor3;
        public byte Sprite0Color;
        public byte Sprite1Color;
        public byte Sprite2Color;
        public byte Sprite3Color;
        public byte Sprite4Color;
        public byte Sprite5Color;
        public byte Sprite6Color;
        public byte Sprite7Color;
        public byte SpriteMultiColor0;
        public byte SpriteMultiColor1;
        public int ScrollX;
        public int ScrollY;
        public bool ColMode40;
        public bool RowMode25;

        public object Clone()
        {
            return MemberwiseClone();
        }
    }

    public Dictionary<int, ScreenLineData> ScreenLineIORegisterValues { get; private set; } = default!;

    public Vic2ScreenLayouts ScreenLayouts { get; private set; } = default!;
    public IVic2SpriteManager SpriteManager { get; private set; } = default!;
    public Vic2CharsetManager CharsetManager { get; private set; } = default!;
    public Vic2BitmapManager BitmapManager { get; private set; } = default!;

    private Vic2() { }

    public static Vic2 BuildVic2(Vic2ModelBase vic2Model, C64 c64)
    {
        var vic2Mem = CreateVic2Memory(c64);

        var vic2IRQ = new Vic2IRQ();

        var screenLineData = BuildScreenLineDataLookup(vic2Model);

        var vic2 = new Vic2()
        {
            C64 = c64,
            Vic2Mem = vic2Mem,
            Vic2Model = vic2Model,
            Vic2IRQ = vic2IRQ,
            ScreenLineIORegisterValues = screenLineData,
        };

        var vic2Screen = new Vic2Screen(vic2Model, c64.CpuFrequencyHz);
        vic2.Vic2Screen = vic2Screen;

        var vic2ScreenLayouts = new Vic2ScreenLayouts(vic2);
        vic2.ScreenLayouts = vic2ScreenLayouts;

        var spriteManager = new Vic2SpriteManager(vic2);
        vic2.SpriteManager = spriteManager;

        var charsetManager = new Vic2CharsetManager(vic2);
        vic2.CharsetManager = charsetManager;

        var bitmapManager = new Vic2BitmapManager(vic2);
        vic2.BitmapManager = bitmapManager;

        return vic2;
    }

    public void MapIOLocations(Memory c64Mem)
    {
        // TODO: Map all IO locations to default behavior with read/write to IOStorage.
        // TODO: Make common init like this that covers all IO locations, not only VIC2: SID, CIA, etc.
        //       Then all that need specific logic writes special mapping below (will overwrite above).

        // Address 0xd011: "Vertical Fine Scrollling and Screen Control Register"
        c64Mem.MapReader(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, ScrCtrlReg1Load);
        c64Mem.MapWriter(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, ScrCtrlReg1Store);

        // Address 0xd012: "Current Raster Line"
        c64Mem.MapReader(Vic2Addr.CURRENT_RASTER_LINE, RasterLoad);
        c64Mem.MapWriter(Vic2Addr.CURRENT_RASTER_LINE, RasterStore);

        // Address 0xd015: "Sprite enable"
        c64Mem.MapReader(Vic2Addr.SPRITE_ENABLE, SpriteEnableLoad);
        c64Mem.MapWriter(Vic2Addr.SPRITE_ENABLE, SpriteEnableStore);

        // Address 0xd016: "Horizontal Fine Scrolling and Screen Control Register"
        c64Mem.MapReader(Vic2Addr.SCROLL_X_AND_SCREEN_CONTROL_REGISTER, ScrollXLoad);
        c64Mem.MapWriter(Vic2Addr.SCROLL_X_AND_SCREEN_CONTROL_REGISTER, ScrollXStore);

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
        // Address 0xd021: Background color 0
        c64Mem.MapReader(Vic2Addr.BACKGROUND_COLOR_0, BackgroundColorLoad);
        c64Mem.MapWriter(Vic2Addr.BACKGROUND_COLOR_0, BackgroundColorStore);
        // Address 0xd022: Background color 1
        c64Mem.MapReader(Vic2Addr.BACKGROUND_COLOR_1, BackgroundColorLoad);
        c64Mem.MapWriter(Vic2Addr.BACKGROUND_COLOR_1, BackgroundColorStore);
        // Address 0xd023: Background color 2
        c64Mem.MapReader(Vic2Addr.BACKGROUND_COLOR_2, BackgroundColorLoad);
        c64Mem.MapWriter(Vic2Addr.BACKGROUND_COLOR_2, BackgroundColorStore);
        // Address 0xd024: Background color 3
        c64Mem.MapReader(Vic2Addr.BACKGROUND_COLOR_3, BackgroundColorLoad);
        c64Mem.MapWriter(Vic2Addr.BACKGROUND_COLOR_3, BackgroundColorStore);

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

        // Addresses 0xd800 - 0xdbe7:  Color RAM is always at fixed location. 1 byte per character in screen ram = 0x03e8 (1000) bytes)
        for (ushort address = 0xd800; address <= 0xdbe7; address++)
        {
            c64Mem.MapReader(address, ColorRAMLoad);
            c64Mem.MapWriter(address, ColorRAMStore);
        }

        // Address 0xdd00: "CIA 2 Port A" (VIC2 bank, serial bus, etc) actually belongs to CIA chip, but as it affects VIC2 bank selection it's added here
        c64Mem.MapReader(CiaAddr.CIA2_DATAA, CIA2PortALoad);
        c64Mem.MapWriter(CiaAddr.CIA2_DATAA, CIA2PortAStore);
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

            if (DisplayMode == DispMode.Text)
                CharsetManager.DetectChangesToCharacterData(vic2Address.Value, value);
            else if (DisplayMode == DispMode.Bitmap)
                BitmapManager.DetectChangesToBitmapData(vic2Address.Value, value);
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

    public void SpriteEnableStore(ushort address, byte value)
    {
        C64.WriteIOStorage(address, value);
    }
    public byte SpriteEnableLoad(ushort address)
    {
        return C64.ReadIOStorage(address);
    }

    public void SpriteMultiColorEnableStore(ushort address, byte value)
    {
        var originalValue = C64.ReadIOStorage(address);
        C64.WriteIOStorage(address, value);
        for (int spriteNumber = 0; spriteNumber < 8; spriteNumber++)
        {
            if (originalValue.IsBitSet(spriteNumber) != value.IsBitSet(spriteNumber))
                SpriteManager.Sprites[spriteNumber].HasChanged(Vic2SpriteChangeType.All);
        }
    }
    public byte SpriteMultiColorEnableLoad(ushort address)
    {
        return C64.ReadIOStorage(address);
    }

    public void SpriteToSpriteCollisionStore(ushort address, byte value)
    {
        // TODO: Is it supposed to be able to write to this value from programs
    }

    public byte SpriteToSpriteCollisionLoad(ushort address)
    {
        var val = SpriteManager.SpriteToSpriteCollisionStore;
        SpriteManager.SpriteToSpriteCollisionStore = 0; // Collision state is cleared after reading
        SpriteManager.SpriteToSpriteCollisionIRQBlock = false; // Enable IRQs to be able to triggered again
        return val;
    }

    public void SpriteToBackgroundCollisionStore(ushort address, byte value)
    {
        // TODO: Is it supposed to be able to write to this value from programs
    }
    public byte SpriteToBackgroundCollisionLoad(ushort address)
    {
        var val = SpriteManager.SpriteToBackgroundCollisionStore;
        SpriteManager.SpriteToBackgroundCollisionStore = 0; // Collision state is cleared after reading
        SpriteManager.SpriteToBackgroundCollisionIRQBlock = false; // Enable IRQs to be able to triggered again
        return val;
    }

    public void BorderColorStore(ushort address, byte value)
    {
        C64.WriteIOStorage(address, (byte)(value & 0b0000_1111));   // Only bits 0-3 are stored;
    }
    public byte BorderColorLoad(ushort address)
    {
        return (byte)(C64.ReadIOStorage(address) | 0b1111_0000); // Bits 4-7 are unused and always 1
    }
    public void BackgroundColorStore(ushort address, byte value)
    {
        C64.WriteIOStorage(address, (byte)(value & 0b0000_1111)); ; // Only bits 0-3 are stored
    }
    public byte BackgroundColorLoad(ushort address)
    {
        return (byte)(C64.ReadIOStorage(address) | 0b1111_0000); // Bits 4-7 are unused and always 1
    }

    public void SpriteColorStore(ushort address, byte value)
    {
        C64.WriteIOStorage(address, (byte)(value & 0b0000_1111));   // Only bits 0-3 are stored;
        var spriteNumber = (address - Vic2Addr.SPRITE_0_COLOR);
        SpriteManager.Sprites[spriteNumber].HasChanged(Vic2SpriteChangeType.Color);
    }
    public byte SpriteColorLoad(ushort address)
    {
        return (byte)(C64.ReadIOStorage(address) | 0b1111_0000); // Bits 4-7 are unused and always 1
    }

    public void SpriteMultiColor0Store(ushort address, byte value)
    {
        C64.WriteIOStorage(address, (byte)(value & 0b0000_1111));   // Only bits 0-3 are stored;
        SpriteManager.SetAllDirty();
    }
    public byte SpriteMultiColor0Load(ushort address)
    {
        return (byte)(C64.ReadIOStorage(address) | 0b1111_0000); // Bits 4-7 are unused and always 1
    }
    public void SpriteMultiColor1Store(ushort address, byte value)
    {
        C64.WriteIOStorage(address, (byte)(value & 0b0000_1111)); ; // Only bits 0-3 are stored
        SpriteManager.SetAllDirty();
    }
    public byte SpriteMultiColor1Load(ushort address)
    {
        return (byte)(C64.ReadIOStorage(address) | 0b1111_0000); // Bits 4-7 are unused and always 1
    }

    public void ColorRAMStore(ushort address, byte value)
    {
        C64.WriteIOStorage(address, (byte)(value & 0b0000_1111));   // Only bits 0-3 are stored;
    }
    public byte ColorRAMLoad(ushort address)
    {
        return C64.ReadIOStorage(address);
    }

    public void ScrollXStore(ushort address, byte value)
    {
        C64.WriteIOStorage(address, (byte)(value & 0b0011_1111)); // Only bits 0-5 are stored
        SpriteManager.SetAllDirty();
    }
    public byte ScrollXLoad(ushort address)
    {
        return (byte)(C64.ReadIOStorage(address) | 0b1100_0000); // Bits 6-7 are unused and always 1
    }

    public void MemorySetupStore(ushort address, byte value)
    {
        C64.WriteIOStorage(address, (byte)(value | 0b0000001)); // Set unused bit 0 to 1 (as it seems to be on a real C64)

        // 0xd018, bit 0: Unused

        // 0xd018, bits 1-3: Text character dot-data base address within VIC-II address space.
        //                   In text mode, this contains the pixels of each character.
        //                   In bitmap mode (normal & multicolor), this contains the pixels of the bitmap (where only bit 3 is significant).
        var characterDotDataAddressSetting = (byte)((value & 0b00001110) >> 1);
        CharsetManager.CharsetBaseAddressUpdate(characterDotDataAddressSetting);
        BitmapManager.BitmapBaseAddressUpdate(characterDotDataAddressSetting);

        // 0xd018, bits 4-7: Video matrix base address within VIC-II address space.
        //                   In text mode, this contains the screen characters
        //                   In bitmap mode, this contains the color information for the screen.
        //                   In multi-color bitmap mode, this also contains the color information for the screen, along with color from Color RAM.
        //                   In all modes, it also contains the sprite pointers.
        // ---------------------------------------------------------------------------
        var videoMatrixBaseAddressSetting = (byte)((value & 0b11110000) >> 4);
        VideoMatrixBaseAddressUpdate(videoMatrixBaseAddressSetting);
    }

    private void VideoMatrixBaseAddressUpdate(byte videoMatrixBaseAddressSetting)
    {
        // From VIC 2 perspective, IO address 0xd018 bits 4-7 controls where within a VIC 2 "Bank"
        // the text screen and sprite pointers are defined. It's a offset from the start of VIC 2 memory.
        // 
        // The parameter videoMatrixBaseAddressSetting contains that 4-bit value.
        // 
        // %0000, 0: Screen: 0x0000-0x03E7, 0-999.          Sprite Pointers: 0x03F8-0x03FF, 1016-1023.
        // %0001, 1: Screen: 0x0400-0x07E7, 1024-2023.      Sprite Pointers: 0x07F8-0x07FF, 2040-2047.      (default)
        // %0010, 2: Screen: 0x0800-0x0BE7, 2048-3047.      Sprite Pointers: 0x0BF8-0x0BFF, 3064-3071.
        // %0011, 3: Screen: 0x0C00-0x0FE7, 3072-4071.      Sprite Pointers: 0x0FF8-0x0FFF, 4088-4095.
        // %0100, 4: Screen: 0x1000-0x13E7, 4096-5095.      Sprite Pointers: 0x13F8-0x13FF, 5112-5119.
        // %0101, 5: Screen: 0x1400-0x17E7, 5120-6123.      Sprite Pointers: 0x17F8-0x17FF, 6136-6143.
        // %0110, 6: Screen: 0x1800-0x1BE7, 6144-7143.      Sprite Pointers: 0x1BF8-0x1BFF, 7160-7167.
        // %0111, 7: Screen: 0x1C00-0x1FE7, 7168-8167.      Sprite Pointers: 0x1FF8-0x1FFF, 8184-8191.
        // %1000, 8: Screen: 0x2000-0x23E7, 8192-9191.      Sprite Pointers: 0x23F8-0x23FF, 9208-9215.
        // %1001, 9: Screen: 0x2400-0x27E7, 9216-10215.     Sprite Pointers: 0x27F8-0x27FF, 10232-10239.
        // %1010, A: Screen: 0x2800-0x2BE7, 10240-11239.    Sprite Pointers: 0x2BF8-0x2BFF, 11256-11263.
        // %1011, B: Screen: 0x2C00-0x2FE7, 11264-12263.    Sprite Pointers: 0x2FF8-0x2FFF, 12280-12287.
        // %1100, C: Screen: 0x3000-0x33E7, 12288-13287.    Sprite Pointers: 0x33F8-0x33FF, 13304-13311.
        // %1101, D: Screen: 0x3400-0x37E7, 13312-14311.    Sprite Pointers: 0x37F8-0x37FF, 14328-14335.
        // %1110, E: Screen: 0x3800-0x3BE7, 14336-15335.    Sprite Pointers: 0x3BF8-0x3BFF, 15352-15359.
        // %1111, F: Screen: 0x3C00-0x3FE7, 15336-16335.    Sprite Pointers: 0x3FF8-0x3FFF, 16352-16359.

        var oldVideoMatrixBaseAddress = VideoMatrixBaseAddress;
        VideoMatrixBaseAddress = (ushort)(videoMatrixBaseAddressSetting * 0x400);
        if (oldVideoMatrixBaseAddress != VideoMatrixBaseAddress)
        {
            SpriteManager.SetAllDirty();
        }
    }

    public byte MemorySetupLoad(ushort address)
    {
        return C64.ReadIOStorage(address);
    }

    public void CIA2PortAStore(ushort address, byte value)
    {
        C64.WriteIOStorage(address, value);

        // --- VIC 2 BANKS ---
        // Bits 0-1 of CIA 2 Port A (0xdd00) selects VIC2 bank (inverted order, the highest value 3 means bank 0, and value 0 means bank 3)
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
        if (CurrentVIC2Bank != oldVIC2Bank)
        {
            Vic2Mem.SetMemoryConfiguration(CurrentVIC2Bank);

            CharsetManager.NotifyCharsetAddressChanged();
            SpriteManager.SetAllDirty();
        }
    }

    public byte CIA2PortALoad(ushort address)
    {
        return C64.ReadIOStorage(address);
    }

    public void ScrCtrlReg1Store(ushort address, byte value)
    {
        C64.WriteIOStorage(address, (byte)(value & 0b0111_1111));

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
            throw new DotNet6502Exception($"Internal error. Setting unreachable scan line for IRQ: {Vic2IRQ.ConfiguredIRQRasterLine}. Incorrect ROM for Vic2 model: {Vic2Model.Name} ?");
#endif

    }

    public byte ScrCtrlReg1Load(ushort address)
    {
        // As the seventh bit in this register (SCRCTRL1), the highest (eigth) bit of the current raster line is returned.
        byte bit7HighestRasterLineBit = (byte)((_currentRasterLineInternal & 0b0000_0001_0000_0000) >> 1);
        return (byte)(C64.ReadIOStorage(address) | bit7HighestRasterLineBit);
    }

    public void RasterStore(ushort address, byte value)
    {
        C64.WriteIOStorage(address, value);

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
        //if (newLine >= Vic2Model.TotalHeight)
        //    Debugger.Break(); // Rasterline overflow
        newLine = (ushort)Math.Clamp(newLine, 0, Vic2Model.TotalHeight - 1);

        if (newLine != _currentRasterLineInternal)
        {
#if DEBUG
            if (newLine > Vic2Model.TotalHeight)
                throw new DotNet6502Exception($"Internal error. Unreachable scan line: {newLine}. The CPU probably executed more cycles current frame than allowed.");
#endif

            _currentRasterLineInternal = newLine;

            // Process timers
            if (C64.TimerMode == TimerMode.UpdateEachRasterLine)
                C64.Cia.ProcessTimers(Vic2Model.CyclesPerLine);

            // Check if a IRQ should be issued for current raster line, and issue it.
            RaiseRasterIRQ(cpu);

            // Remember colors and other IO registers for each raster line
            StoreRasterLineIORegisters(_currentRasterLineInternal);
        }

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

    private static Dictionary<int, ScreenLineData> BuildScreenLineDataLookup(Vic2ModelBase vic2Model)
    {
        var screenLineDataLookup = new Dictionary<int, ScreenLineData>();
        for (ushort i = 0; i < vic2Model.TotalHeight; i++)
        {
            //if (!vic2Model.IsRasterLineInMainScreen(i))
            //    continue;
            screenLineDataLookup.Add(i, new ScreenLineData());
        }
        return screenLineDataLookup;
    }

    private void StoreRasterLineIORegisters(ushort rasterLine)
    {
        var screenLine = Vic2Model.ConvertRasterLineToScreenLine(rasterLine);
        ScreenLineData screenLineData = ScreenLineIORegisterValues[screenLine];

        screenLineData.BorderColor = C64.ReadIOStorage(Vic2Addr.BORDER_COLOR);
        screenLineData.BackgroundColor0 = C64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_0);
        screenLineData.BackgroundColor1 = C64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_1);
        screenLineData.BackgroundColor2 = C64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_2);
        screenLineData.BackgroundColor3 = C64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_3);
        screenLineData.Sprite0Color = C64.ReadIOStorage(Vic2Addr.SPRITE_0_COLOR);
        screenLineData.Sprite1Color = C64.ReadIOStorage(Vic2Addr.SPRITE_1_COLOR);
        screenLineData.Sprite2Color = C64.ReadIOStorage(Vic2Addr.SPRITE_2_COLOR);
        screenLineData.Sprite3Color = C64.ReadIOStorage(Vic2Addr.SPRITE_3_COLOR);
        screenLineData.Sprite4Color = C64.ReadIOStorage(Vic2Addr.SPRITE_4_COLOR);
        screenLineData.Sprite5Color = C64.ReadIOStorage(Vic2Addr.SPRITE_5_COLOR);
        screenLineData.Sprite6Color = C64.ReadIOStorage(Vic2Addr.SPRITE_6_COLOR);
        screenLineData.Sprite7Color = C64.ReadIOStorage(Vic2Addr.SPRITE_7_COLOR);
        screenLineData.SpriteMultiColor0 = C64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_0);
        screenLineData.SpriteMultiColor1 = C64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_1);
        screenLineData.ScrollX = GetScrollX();
        screenLineData.ScrollY = GetScrollY();
        screenLineData.ColMode40 = !Is38ColumnDisplayEnabled;
        screenLineData.RowMode25 = !Is24RowDisplayEnabled;
    }
}
