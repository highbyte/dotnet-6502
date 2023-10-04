using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2CharsetManager.CharsetAddressChangedEventArgs;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// Manager for VIC-II character sets
/// </summary>
public class Vic2CharsetManager
{
    public const int CHARACTERSET_NUMBER_OF_CHARCTERS = 256;
    public const int CHARACTERSET_ONE_CHARACTER_BYTES = 8;      // 8 bytes (one line per byte) for each character.
    public const int CHARACTERSET_SIZE = CHARACTERSET_NUMBER_OF_CHARCTERS * CHARACTERSET_ONE_CHARACTER_BYTES;    // = 2KB -> 2048 (0x0800) bytes. 256 characters, where each character takes up 8 bytes (1 byte per character line)

    public Vic2 Vic2 { get; private set; }
    private Memory _vic2Mem => Vic2.Vic2Mem;

    // Offset into the currently selected VIC2 bank (Mem.SetMemoryConfiguration(bank))
    private ushort _characterSetAddressInVIC2Bank;
    public ushort CharacterSetAddressInVIC2Bank => _characterSetAddressInVIC2Bank;
    // True if CharacterSetAddressInVIC2Bank points to location where Chargen ROM (two charsets, unshifted & shifted) is "shadowed".
    public bool CharacterSetAddressInVIC2BankIsChargenROMShifted => _characterSetAddressInVIC2Bank == 0x1000;
    public bool CharacterSetAddressInVIC2BankIsChargenROMUnshifted => _characterSetAddressInVIC2Bank == 0x1800;


    private Vic2CharsetManager() { }

    public Vic2CharsetManager(Vic2 vic2)
    {
        Vic2 = vic2;
    }

    public void NotifyCharsetAddressChanged()
    {
        OnCharsetAddressChanged(new(charsetChangeType: CharsetChangeType.CharacterSetBaseAddress));
    }

    public void CharsetBaseAddressUpdate(byte characterBaseAddressSetting)
    {
        // From VIC 2 perspective, IO address 0xd018 bits 1-3 controls where within a VIC 2 "Bank"
        // the character set is read from. It's a offset from the start of VIC 2 memory.
        // 
        // The parameter characterBaseAddressSetting contains that 3-bit value.
        // 
        // By default, this value is 2 (%010), which is 0x1000-0x17ff offset from Bank below. 
        // If Bank 0 or bank 2 is selected, this points to a shadow copy of the Chargen ROM
        // %000, 0: 0x0000-0x07FF, 0-2047.
        // %001, 1: 0x0800-0x0FFF, 2048-4095.
        // %010, 2: 0x1000-0x17FF, 4096-6143.
        // %011, 3: 0x1800-0x1FFF, 6144-8191.
        // %100, 4: 0x2000-0x27FF, 8192-10239.
        // %101, 5: 0x2800-0x2FFF, 10240-12287.
        // %110, 6: 0x3000-0x37FF, 12288-14335.
        // %111, 7: 0x3800-0x3FFF, 14336-16383.
        // 
        var oldCharacterSetAddressInVIC2Bank = _characterSetAddressInVIC2Bank;
        _characterSetAddressInVIC2Bank = characterBaseAddressSetting switch
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
        if (_characterSetAddressInVIC2Bank != oldCharacterSetAddressInVIC2Bank)
            NotifyCharsetAddressChanged();
    }

    public void DetectChangesToCharacterData(ushort vic2Address, byte value)
    {
        // TODO: Temp fix for this being called during startup before VIC2 bank is set.
        if (CharacterSetAddressInVIC2Bank == 0)
            return;

        // Detect changes to the data within the current charset.
        if (vic2Address >= CharacterSetAddressInVIC2Bank && vic2Address < CharacterSetAddressInVIC2Bank + CHARACTERSET_SIZE)
        {
            // If changes is done, but the current charset one of the "shadowed" Chargen ROM location, then we cannot change the charset.
            if (CharacterSetAddressInVIC2BankIsChargenROMShifted || CharacterSetAddressInVIC2BankIsChargenROMUnshifted)
                return;

            // Check if value actually changed
            if (_vic2Mem[vic2Address] != value)
            {
                // Raise event for the character and which line in it that changed
                var charCode = (byte)((vic2Address - CharacterSetAddressInVIC2Bank) / CHARACTERSET_ONE_CHARACTER_BYTES);
                OnCharsetAddressChanged(new(
                    charsetChangeType: CharsetChangeType.CharacterSetCharacter,
                    charCode: charCode));
            }
        }
    }

    public event EventHandler<CharsetAddressChangedEventArgs> CharsetAddressChanged;
    protected virtual void OnCharsetAddressChanged(CharsetAddressChangedEventArgs e)
    {
        var handler = CharsetAddressChanged;
        handler?.Invoke(this, e);
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
        var characterAddress = (ushort)(Vic2.VideoMatrixBaseAddress + (characterRow * Vic2.Vic2Screen.TextCols) + characterCol);
        var characterCode = _vic2Mem[characterAddress];
        var characterSetLineAddress = (ushort)(CharacterSetAddressInVIC2Bank + (characterCode * Vic2.Vic2Screen.CharacterHeight) + line);
        return _vic2Mem[characterSetLineAddress];
    }

    public class CharsetAddressChangedEventArgs : EventArgs
    {
        public CharsetChangeType ChangeType { get; private set; }

        public byte? CharCode { get; private set; }
        public enum CharsetChangeType
        {
            CharacterSetBaseAddress,
            CharacterSetCharacter
        }

        public CharsetAddressChangedEventArgs(CharsetChangeType charsetChangeType, byte? charCode = null)
        {
            ChangeType = charsetChangeType;
            CharCode = charCode;
        }
    }
}
