using Highbyte.DotNet6502.Systems.Commodore64.Models;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video
{
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
        public Vic2ModelBase Vic2Model { get; private set; }
        public Memory Mem { get; set; }

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

        public event EventHandler<CharsetAddressChangedEventArgs> CharsetAddressChanged;
        protected virtual void OnCharsetAddressChanged(CharsetAddressChangedEventArgs e)
        {
            var handler = CharsetAddressChanged;
            handler?.Invoke(this, e);
        }

        private Vic2() { }

        public static Vic2 BuildVic2(byte[] ram, Dictionary<string, byte[]> romData, Vic2ModelBase vic2Model)
        {
            var vic2Mem = CreateSid2Memory(ram, romData);

            var vic2 = new Vic2()
            {
                Mem = vic2Mem,
                Vic2Model = vic2Model,
            };

            return vic2;
        }

        /// <summary>
        /// </summary>
        /// <param name="ram"></param>
        /// <param name="roms"></param>
        /// <returns></returns>
        private static Memory CreateSid2Memory(byte[] ram, Dictionary<string, byte[]> romData)
        {
            var chargen = romData["chargen"];

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

        public void VerticalBlank(CPU cpu)
        {
            // Issue vertical blank signal to CPU (typically issue a IRQ, will take effect next frame)
            // TODO: This assumes the IRQ always occurs for raster line 0. In a real Vic2 (C64) the line to generate the IRQ is configurable in 0xd012
            cpu.IRQ = true;
        }

        public void CPUCyclesConsumed(CPU cpu, Memory mem, ulong cyclesConsumed)
        {
            CyclesConsumedCurrentVblank += cyclesConsumed;
            //var cyclesUntilVBlank = VariantSetting.CyclesPerLine * (VariantSetting.Lines - (VariantSetting.VBlankLines / 2));
            var cyclesUntilVBlank = Vic2Model.CyclesPerFrame;
            if (CyclesConsumedCurrentVblank >= cyclesUntilVBlank)
            {
                CyclesConsumedCurrentVblank = 0;
                VerticalBlank(cpu);
            }
            UpdateCurrentRasterLine(mem, CyclesConsumedCurrentVblank);
        }

        private void UpdateCurrentRasterLine(Memory mem, ulong cyclesConsumedCurrentVblank)
        {
            // Calculate the current raster line based on how man CPU cycles has been executed this frame
            var line = (cyclesConsumedCurrentVblank / Vic2Model.CyclesPerLine);
            // Bits 0-7 of current line stored in 0xd012
            mem[Vic2Addr.CURRENT_RASTER_LINE] = (byte)(line & 0xff);
            // Bit 8 of current line stored in 0xd011 bit #7
            var screenControlReg1Value = mem[Vic2Addr.SCREEN_CONTROL_REGISTER_1];
            if (line > Vic2Model.Lines)
                throw new Exception($"Internal error. Unreachable scan line: {line}. The CPU probably executed more cycles current frame than allowed.");
            if (line <= 255)
                screenControlReg1Value.ClearBit(7);
            else
                screenControlReg1Value.SetBit(7);
            mem[Vic2Addr.SCREEN_CONTROL_REGISTER_1] = screenControlReg1Value;
        }
    }
}
