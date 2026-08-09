using Highbyte.DotNet6502.Systems.Apple2.Disk2;

namespace Highbyte.DotNet6502.Systems.Apple2.Peripherals;

/// <summary>
/// The Apple II memory-mapped I/O page at $C000-$C0FF plus the peripheral-slot ROM space at
/// $C100-$CFFF.
///
/// Apple II soft switches respond to <em>any</em> bus access, so a read and a write to the same
/// address have the same side effect; only the value returned to the CPU differs. Addresses in
/// $C000-$C07F are decoded on bits 7-4 only (each switch occupies 16 consecutive addresses),
/// which is why software may equivalently use e.g. $C010 or $C01F to clear the keyboard strobe.
///
/// Scope: keyboard ($C000 / $C010), display-mode switches, everything else answering as
/// unconnected. Nothing here generates an interrupt — the machine has no timer.
/// </summary>
public class Apple2SoftSwitches
{
    /// <summary>Keyboard data + strobe ($C000-$C00F).</summary>
    public const ushort KeyboardDataAddress = 0xC000;
    /// <summary>Clear keyboard strobe ($C010-$C01F).</summary>
    public const ushort KeyboardStrobeClearAddress = 0xC010;
    /// <summary>Speaker toggle ($C030-$C03F).</summary>
    public const ushort SpeakerToggleAddress = 0xC030;

    public const ushort GraphicsModeAddress = 0xC050;
    public const ushort TextModeAddress = 0xC051;
    public const ushort MixedModeOffAddress = 0xC052;
    public const ushort MixedModeOnAddress = 0xC053;
    public const ushort TextPage1Address = 0xC054;
    public const ushort TextPage2Address = 0xC055;
    public const ushort LoResModeAddress = 0xC056;
    public const ushort HiResModeAddress = 0xC057;

    public const ushort IoPageStartAddress = 0xC000;
    public const ushort IoPageEndAddress = 0xC0FF;
    public const ushort SlotSpaceStartAddress = 0xC100;
    public const ushort SlotSpaceEndAddress = 0xCFFF;

    /// <summary>
    /// Value returned by addresses with no device behind them. Real hardware returns the
    /// "floating bus" (whatever the video circuitry last fetched); $FF is the conventional
    /// emulator stand-in and is what makes the Autostart ROM's slot scan fail cleanly so it
    /// falls through to BASIC instead of trying to boot a disk.
    /// </summary>
    public const byte UnconnectedReadValue = 0xFF;

    /// <summary>Language card bank switching ($C080-$C08F, slot 0).</summary>
    public const ushort LanguageCardAddress = 0xC080;

    private readonly Apple2Keyboard _keyboard;
    private readonly Disk2Controller? _diskController;
    private readonly Apple2GamePort? _gamePort;
    private readonly Apple2Speaker? _speaker;
    private readonly Apple2LanguageCard? _languageCard;

    public Apple2SoftSwitches(
        Apple2Keyboard keyboard,
        Disk2Controller? diskController = null,
        Apple2GamePort? gamePort = null,
        Apple2Speaker? speaker = null,
        Apple2LanguageCard? languageCard = null)
    {
        _keyboard = keyboard;
        _diskController = diskController;
        _gamePort = gamePort;
        _speaker = speaker;
        _languageCard = languageCard;
    }

    /// <summary>
    /// $C070 (PTRIG) restarts every paddle's one-shot. Reads and writes both strobe it, which the
    /// generic write-forwards-to-read path below already gives us.
    /// </summary>
    private byte TriggerPaddles()
    {
        _gamePort?.Trigger();
        return 0x00;
    }

    /// <summary>Text mode ($C051) vs. graphics mode ($C050).</summary>
    public bool TextMode { get; private set; } = true;

    /// <summary>Mixed text/graphics ($C053) vs. full screen ($C052).</summary>
    public bool MixedMode { get; private set; }

    /// <summary>Display page 2 ($C055) vs. page 1 ($C054).</summary>
    public bool Page2 { get; private set; }

    /// <summary>Hi-res ($C057) vs. lo-res ($C056) graphics.</summary>
    public bool HiRes { get; private set; }

    /// <summary>
    /// Number of $C030 accesses. Delegates to the speaker when one is attached, so there is a
    /// single count rather than two that can disagree.
    /// </summary>
    public ulong SpeakerToggleCount => _speaker?.ToggleCount ?? _speakerTogglesWithoutSpeaker;

    private ulong _speakerTogglesWithoutSpeaker;

    /// <summary>Base address of the text page the display switches currently select.</summary>
    public ushort ActiveTextPageBaseAddress => Page2
        ? Video.Apple2TextScreen.TextPage2BaseAddress
        : Video.Apple2TextScreen.TextPage1BaseAddress;

    /// <summary>Base address of the hi-res page the display switches currently select.</summary>
    public ushort ActiveHiResPageBaseAddress => Page2
        ? Video.Apple2HiResScreen.HiResPage2BaseAddress
        : Video.Apple2HiResScreen.HiResPage1BaseAddress;

    /// <summary>Wires the I/O page and slot space into the memory map.</summary>
    public void MapIOLocations(Memory mem)
    {
        for (var address = IoPageStartAddress; address <= IoPageEndAddress; address++)
        {
            mem.MapReader(address, Read);
            mem.MapWriter(address, Write);
        }

        // Empty peripheral slots: reads return the unconnected value, writes go nowhere.
        for (var address = SlotSpaceStartAddress; address <= SlotSpaceEndAddress; address++)
        {
            mem.MapReader(address, static _ => UnconnectedReadValue);
            mem.MapWriter(address, static (_, _) => { });
        }

        // Slot 6 ROM space ($C600-$C6FF): the Disk II boot ROM, visible only while the
        // controller is enabled (boot ROM configured and a disk inserted) so the Autostart
        // slot scan otherwise falls through to BASIC.
        if (_diskController != null)
        {
            for (var address = Disk2Controller.BootRomBaseAddress;
                address < Disk2Controller.BootRomBaseAddress + Disk2Controller.BootRomSize;
                address++)
            {
                mem.MapReader(address, _diskController.ReadBootRom);
            }
        }
    }

    /// <summary>Applies the side effect of an access and returns the value the CPU reads.</summary>
    public byte Read(ushort address) => Access(address, isRead: true);

    /// <summary>
    /// A write triggers the same side effects as a read and the value is ignored — with one
    /// exception: the language card's write-enable sequence is armed only by reads, so the access
    /// kind has to be carried through rather than folded into <see cref="Read"/>.
    /// </summary>
    public void Write(ushort address, byte value) => Access(address, isRead: false);

    private byte Access(ushort address, bool isRead)
    {
        return (address & 0x00F0) switch
        {
            0x00 => _keyboard.ReadData(),                    // $C000-$C00F  keyboard data + strobe
            0x10 => _keyboard.ReadAndClearStrobe(),          // $C010-$C01F  clear strobe
            0x30 => ToggleSpeaker(),                         // $C030-$C03F  speaker
            0x50 => ApplyDisplaySwitch(address),             // $C050-$C05F  display mode
            0x60 => _gamePort?.ReadGamePort(address) ?? 0x00, // $C060-$C06F  cassette in, buttons, paddles
            0x70 => TriggerPaddles(),                        // $C070-$C07F  PTRIG: restart paddle timers
            0x80 => AccessLanguageCard(address, isRead),     // $C080-$C08F  language card bank switching
            0xE0 => ReadDiskController(address),             // $C0E0-$C0EF  Disk II controller (slot 6)
            _ => UnconnectedReadValue,
        };
    }

    private byte AccessLanguageCard(ushort address, bool isRead)
    {
        _languageCard?.Access(address, isRead);
        return UnconnectedReadValue;
    }

    private byte ReadDiskController(ushort address)
        => _diskController != null && _diskController.IsEnabled
            ? _diskController.BusAccess(address)
            : UnconnectedReadValue;

    private byte ToggleSpeaker()
    {
        if (_speaker != null)
            _speaker.Toggle();
        else
            _speakerTogglesWithoutSpeaker++;

        return UnconnectedReadValue;
    }

    private byte ApplyDisplaySwitch(ushort address)
    {
        // $C050-$C057 are decoded on bits 2-1 (which switch) and bit 0 (off/on).
        if ((address & 0x0008) == 0)
        {
            var on = (address & 0x0001) != 0;
            switch (address & 0x0006)
            {
                case 0x0000: TextMode = on; break;
                case 0x0002: MixedMode = on; break;
                case 0x0004: Page2 = on; break;
                case 0x0006: HiRes = on; break;
            }
        }
        return UnconnectedReadValue;
    }

    public void Reset()
    {
        TextMode = true;
        MixedMode = false;
        Page2 = false;
        HiRes = false;
        _speaker?.Reset();
        _speakerTogglesWithoutSpeaker = 0;
    }

    // --- Snapshot support (consumed by the apple2-core snapshot module in the same assembly) ---

    /// <summary>
    /// Restores the four display switches. These are the whole of the machine's video state: unlike
    /// the C64's VIC-II there are no registers to re-derive anything from, because the switches are
    /// write-only flip-flops that live nowhere in the address space. Restoring them is what makes a
    /// snapshot taken in hi-res come back in hi-res rather than at the text prompt.
    /// </summary>
    internal void RestoreSnapshotDisplaySwitches(bool textMode, bool mixedMode, bool page2, bool hiRes)
    {
        TextMode = textMode;
        MixedMode = mixedMode;
        Page2 = page2;
        HiRes = hiRes;
    }
}
