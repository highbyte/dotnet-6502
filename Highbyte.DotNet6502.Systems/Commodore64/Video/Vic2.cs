using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// --- CHARACTER GENERATOR ROM ---
/// From the CPU perspective, the character generator ROM (chargen) lives at addresses 0xd000 - 0xdfff.
/// But only if that is enabled by C64 IO port bank layout at address 0x0001.
/// By default, the CPU sees IO control addresses in that range.
/// 
/// The VIC 2 sees the character set differently (below).
///
/// </summary>
public class Vic2
{ 
    public C64 C64 { get; private set; }
    public Vic2ModelBase Vic2Model { get; private set; }
    public Memory Mem { get; private set; }

    public Vic2IRQ Vic2IRQ { get; private set; }

    public const ushort COLS = 40;      // # characters per line in text mode
    public const ushort ROWS = 25;      // # rows in text mode
    public const ushort WIDTH = 320;    // # pixels in drawable area (text mode and bitmap graphics mode)
    public const ushort HEIGHT = 200;   // # pixels in drawable area  (text mode and bitmap graphics mode)

    public const int CHARACTERSET_NUMBER_OF_CHARCTERS = 256;
    public const int CHARACTERSET_ONE_CHARACTER_BYTES = 8;      // 8 bytes (one line per byte) for each character.
    public const int CHARACTERSET_SIZE = CHARACTERSET_NUMBER_OF_CHARCTERS * CHARACTERSET_ONE_CHARACTER_BYTES;    // = 1024 (0x0400) bytes. 256 characters, where each character takes up 8 bytes (1 byte per character line)


    public ulong CyclesConsumedCurrentVblank { get; private set; } = 0;

    public byte CurrentVIC2Bank { get; private set; }
    private ushort _currentVIC2BankOffset = 0;

    // Offset into the currently selected VIC2 bank (Mem.SetMemoryConfiguration(bank))
    public ushort CharacterSetAddressInVIC2Bank => _currentVIC2BankOffset;
    // True if CharacterSetAddressInVIC2Bank points to location where Chargen ROM (two charsets, unshifted & shifted) is "shadowed".
    public bool CharacterSetAddressInVIC2BankIsChargenROMShifted => _currentVIC2BankOffset == 0x1000;
    public bool CharacterSetAddressInVIC2BankIsChargenROMUnshifted => _currentVIC2BankOffset == 0x1800;

    public byte BorderColor { get; private set; }
    public byte BackgroundColor { get; private set; }
    public byte MemorySetup { get; private set; }
    public byte PortA { get; private set; }

    public byte ScrCtrl1 { get; private set; }

    private ushort _currentRasterLineInternal = ushort.MaxValue;
    public ushort CurrentRasterLine => _currentRasterLineInternal;


    public event EventHandler<CharsetAddressChangedEventArgs> CharsetAddressChanged;
    protected virtual void OnCharsetAddressChanged(CharsetAddressChangedEventArgs e)
    {
        var handler = CharsetAddressChanged;
        handler?.Invoke(this, e);
    }

    public Dictionary<ushort, byte> ScreenLineBorderColor { get; private set; }
    public Dictionary<ushort, byte> ScreenLineBackgroundColor { get; private set; }

    private Vic2() { }

    public static Vic2 BuildVic2(byte[] ram, Dictionary<string, byte[]> romData, Vic2ModelBase vic2Model, C64 c64)
    {
        var vic2Mem = CreateVic2Memory(ram, romData);

        var screenLineBorderColorLookup = InitializeScreenLineBorderColorLookup(vic2Model);
        var screenLineBackgroundColorLookup = InitializeScreenLineBackgroundColorLookup(c64, vic2Model);

        var vic2IRQ = new Vic2IRQ();

        var vic2 = new Vic2()
        {
            C64 = c64,
            Mem = vic2Mem,
            Vic2Model = vic2Model,
            Vic2IRQ = vic2IRQ,
            ScreenLineBorderColor = screenLineBorderColorLookup,
            ScreenLineBackgroundColor = screenLineBackgroundColorLookup
        };

        return vic2;
    }

    private static Dictionary<ushort, byte> InitializeScreenLineBorderColorLookup(Vic2ModelBase vic2Model)
    {
        var screenLineBorderColor = new Dictionary<ushort, byte>();
        for (ushort i = 0; i < vic2Model.Lines; i++)
        {
            screenLineBorderColor.Add(i, 0);
        }
        return screenLineBorderColor;
    }

    private static Dictionary<ushort, byte> InitializeScreenLineBackgroundColorLookup(C64 c64, Vic2ModelBase vic2Model)
    {
        var screenLineBackgroundColor = new Dictionary<ushort, byte>();
        for (ushort i = 0; i < vic2Model.Lines; i++)
        {
            if (!IsRasterLineInMainScreen(c64, vic2Model, i))
                continue;
            screenLineBackgroundColor.Add(i, 0);
        }
        return screenLineBackgroundColor;
    }


    public void MapIOLocations(Memory mem)
    {
        // Address 0xd011: "Screen Control Register 1"
        mem.MapReader(Vic2Addr.SCREEN_CONTROL_REGISTER_1, ScrCtrlReg1Load);
        mem.MapWriter(Vic2Addr.SCREEN_CONTROL_REGISTER_1, ScrCtrlReg1Store);

        // Address 0xd012: "Current Raster Line"
        mem.MapReader(Vic2Addr.CURRENT_RASTER_LINE, RasterLoad);
        mem.MapWriter(Vic2Addr.CURRENT_RASTER_LINE, RasterStore);

        // Address 0xd018: "Memory setup" (VIC2 pointer for charset/bitmap & screen memory)
        mem.MapReader(Vic2Addr.MEMORY_SETUP, MemorySetupLoad);
        mem.MapWriter(Vic2Addr.MEMORY_SETUP, MemorySetupStore);

        // Address 0xd019: "VIC Interrupt Flag Register"
        mem.MapReader(Vic2Addr.VIC_IRQ, VICIRQLoad);
        mem.MapWriter(Vic2Addr.VIC_IRQ, VICIRQStore);

        // Address 0xd01a: "IRQ Mask Register"
        mem.MapReader(Vic2Addr.IRQ_MASK, IRQMASKLoad);
        mem.MapWriter(Vic2Addr.IRQ_MASK, IRQMASKStore);

        // Address 0xd020: Border color
        mem.MapReader(Vic2Addr.BORDER_COLOR, BorderColorLoad);
        mem.MapWriter(Vic2Addr.BORDER_COLOR, BorderColorStore);
        // Address 0xd021: Background color
        mem.MapReader(Vic2Addr.BACKGROUND_COLOR, BackgroundColorLoad);
        mem.MapWriter(Vic2Addr.BACKGROUND_COLOR, BackgroundColorStore);

        // Address 0xdd00: "Port A" (VIC2 bank & serial bus)
        mem.MapReader(Vic2Addr.PORT_A, PortALoad);
        mem.MapWriter(Vic2Addr.PORT_A, PortAStore);
    }

    /// <summary>
    /// </summary>
    /// <param name="ram"></param>
    /// <param name="roms"></param>
    /// <returns></returns>
    private static Memory CreateVic2Memory(byte[] ram, Dictionary<string, byte[]> romData)
    {
        var chargen = romData[C64Config.CHARGEN_ROM_NAME];

        // Vic2 can use 4 different banks of 16KB of memory each. They map into C64 RAM or Chargen ROM depending on bank.
        var mem = new Memory(memorySize: 16 * 1024, numberOfConfigurations: 4, mapToDefaultRAM: false);

        mem.SetMemoryConfiguration(0);
        mem.MapRAM(0x0000, ram, 0, 0x1000);
        mem.MapRAM(0x1000, chargen);    // Chargen ROM "shadow" appear here.  Assume chargen is 0x1000 (4096) bytes length.
        mem.MapRAM(0x2000, ram, 0x2000, 0x2000);

        mem.SetMemoryConfiguration(1);
        mem.MapRAM(0x0000, ram, 0x4000, 0x4000);

        mem.SetMemoryConfiguration(2);
        mem.MapRAM(0x0000, ram, 0x8000, 0x1000);
        mem.MapRAM(0x1000, chargen);    // Chargen ROM "shadow" appear here. Assume chargen is 0x1000 (4096) bytes length.
        mem.MapRAM(0x2000, ram, 0xa000, 0x2000);

        mem.SetMemoryConfiguration(3);
        mem.MapRAM(0x0000, ram, 0xc000, 0x4000);

        // Default to bank 0
        mem.SetMemoryConfiguration(0);

        return mem;
    }

    public void BorderColorStore(ushort _, byte value)
    {
        BorderColor = value;
    }
    public byte BorderColorLoad(ushort _)
    {
        return BorderColor;
    }
    public void BackgroundColorStore(ushort _, byte value)
    {
        BackgroundColor = value;
    }
    public byte BackgroundColorLoad(ushort _)
    {
        return BackgroundColor;
    }

    public void MemorySetupStore(ushort _, byte value)
    {
        MemorySetup = value;

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

    public byte MemorySetupLoad(ushort _)
    {
        return MemorySetup;
    }

    public void PortAStore(ushort _, byte value)
    {
        PortA = value;

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
        if (CurrentVIC2Bank != oldVIC2Bank)
            OnCharsetAddressChanged(new());
    }

    public byte PortALoad(ushort _)
    {
        return PortA;
    }

    public void ScrCtrlReg1Store(ushort _, byte value)
    {
        ScrCtrl1 = (byte)(value & 0b0111_1111);

        // If the VIC2 model is PAL, then allow configuring the 8th bit of the raster line IRQ.
        // Note: As the Kernal ROM initializes this 8th bit for both NTSC and PAL (same ROM for both), we need this workaround here.
        // TODO: Should an enum be used for VIC2 model base type (PAL or NTSC)?
        if (Vic2Model.LinesVisible > 256)
        {
            // When writing to this register (SCRCTRL1) the seventh bit is the highest (eigth) for the the raster line IRQ setting.
            ushort bit7HighestRasterLineBitIRQ = (ushort)(value & 0b1000_0000);
            bit7HighestRasterLineBitIRQ = (ushort)(bit7HighestRasterLineBitIRQ << 1);

            if (Vic2IRQ.ConfiguredIRQRasterLine.HasValue)
                Vic2IRQ.ConfiguredIRQRasterLine = (ushort?)(Vic2IRQ.ConfiguredIRQRasterLine | bit7HighestRasterLineBitIRQ);
            else
                Vic2IRQ.ConfiguredIRQRasterLine = 0;
        }

#if DEBUG
        if (Vic2IRQ.ConfiguredIRQRasterLine > Vic2Model.Lines)
            throw new Exception($"Internal error. Setting unreachable scan line for IRQ: {Vic2IRQ.ConfiguredIRQRasterLine}. Incorrect ROM for Vic2 model: {Vic2Model.Name} ?");
#endif

    }

    public byte ScrCtrlReg1Load(ushort _)
    {
        // As the seventh bit in this register (SCRCTRL1), the highest (eigth) bit of the current raster line is returned.
        byte bit7HighestRasterLineBit = (byte)((_currentRasterLineInternal & 0b0000_0001_0000_0000) >> 1);
        return (byte)(ScrCtrl1 | bit7HighestRasterLineBit);
    }

    public void RasterStore(ushort _, byte value)
    {
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
            if (newLine > Vic2Model.Lines)
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
            || (!Vic2IRQ.ConfiguredIRQRasterLine.HasValue & _currentRasterLineInternal >= Vic2Model.Lines))
            && Vic2IRQ.IsEnabled(source)
            && !Vic2IRQ.IsTriggered(source, C64.CPU))
        {
            Vic2IRQ.Trigger(source, cpu);
        }
    }

    private void StoreBorderColorForRasterLine(ushort rasterLine)
    {
        var screenLine = Vic2Model.ConvertRasterLineToScreenLine(rasterLine);
        ScreenLineBorderColor[screenLine] = BorderColor;
    }

    private void StoreBackgroundColorForRasterLine(ushort rasterLine)
    {
        if (!IsRasterLineInMainScreen(C64, C64.Vic2.Vic2Model, rasterLine))
            return;

        var screenLine = Vic2Model.ConvertRasterLineToScreenLine(rasterLine);
        ScreenLineBackgroundColor[screenLine] = BackgroundColor;
    }

    private static bool IsRasterLineInMainScreen(C64 c64, Vic2ModelBase vic2Model, ulong rasterLine)
    {
        return rasterLine >= vic2Model.FirstRasterLineOfMainScreen
            && rasterLine < vic2Model.FirstRasterLineOfMainScreen + (ulong)c64.Height;
    }
}
