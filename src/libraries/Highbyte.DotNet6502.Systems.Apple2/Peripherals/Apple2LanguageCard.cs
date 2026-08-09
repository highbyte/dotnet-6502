namespace Highbyte.DotNet6502.Systems.Apple2.Peripherals;

/// <summary>
/// The Apple Language Card: 16 KB of RAM that overlays the ROM space, switched by the soft switches
/// at $C080-$C08F (slot 0).
///
/// <para>
/// It brings the machine to the 64 KB that ProDOS 8 requires, which is the gate in front of most
/// Apple II software from roughly 1984 onward. A 48 KB machine boots a ProDOS disk only as far as
/// ProDOS's own memory check, which stops with <c>RELOCATION/ CONFIGURATION ERROR</c>.
/// </para>
///
/// <para>
/// <b>Why 16 KB covers a 12 KB address range.</b> $E000-$FFFF is a single 8 KB block, but
/// $D000-$DFFF has <em>two</em> 4 KB banks that are selected independently. 4 + 4 + 8 = 16.
/// </para>
///
/// <para>
/// <b>The write-enable rule.</b> Enabling writes to the card takes <em>two consecutive accesses</em>
/// to an odd switch address, and only a <em>read</em> arms the first one. This is a deliberate
/// hardware guard: a single stray access — an indexed read that happens to land in $C08x, say —
/// must not silently unlock RAM under the ROM. Modelled here with the same pre-write flip-flop the
/// hardware uses, because software does rely on it: the idiom for "write to the card while still
/// executing from ROM" is to read the switch twice in a row.
/// </para>
///
/// <para>
/// Not modelled: the IIe's $C011/$C012 status readback (a IIe feature; the II Plus this machine
/// emulates has no way to read the card's state back), and the IIe's 128 KB auxiliary memory, which
/// is a different machine rather than a bigger II Plus.
/// </para>
/// </summary>
public class Apple2LanguageCard
{
    /// <summary>First soft switch: $C080-$C08F, slot 0.</summary>
    public const ushort IoBaseAddress = 0xC080;

    /// <summary>One 4 KB bank at $D000-$DFFF; there are two of them.</summary>
    public const int BankSize = 0x1000;

    /// <summary>The single 8 KB block at $E000-$FFFF.</summary>
    public const int UpperSize = 0x2000;

    /// <summary>4 KB + 4 KB + 8 KB.</summary>
    public const int RamSize = BankSize * 2 + UpperSize;

    /// <summary>Offset of bank 1's 4 KB within <see cref="Ram"/>.</summary>
    public const int Bank1Offset = 0;

    /// <summary>Offset of bank 2's 4 KB within <see cref="Ram"/>.</summary>
    public const int Bank2Offset = BankSize;

    /// <summary>Offset of the shared $E000-$FFFF block within <see cref="Ram"/>.</summary>
    public const int UpperOffset = BankSize * 2;

    /// <summary>Number of distinct memory maps the switch state can produce.</summary>
    public const int MemoryConfigurationCount = 8;

    private readonly byte[] _ram = new byte[RamSize];

    /// <summary>The card's 16 KB, laid out as bank 1, bank 2, then the shared $E000-$FFFF block.</summary>
    public byte[] Ram => _ram;

    /// <summary>
    /// Whether $D000-$FFFF reads come from the card rather than from ROM. Power-on state is ROM, so
    /// a machine that never touches $C08x behaves exactly like one with no card fitted.
    /// </summary>
    public bool ReadRam { get; private set; }

    /// <summary>Which 4 KB bank is visible at $D000-$DFFF. Power-on selects bank 2, as the hardware does.</summary>
    public bool Bank1Selected { get; private set; }

    /// <summary>Whether writes to $D000-$FFFF reach the card. Requires the two-access sequence described above.</summary>
    public bool WriteEnabled { get; private set; }

    /// <summary>
    /// The pre-write flip-flop: set by a <em>read</em> of an odd switch address, and the reason a
    /// second access is needed before writes are enabled.
    /// </summary>
    public bool PreWrite { get; private set; }

    /// <summary>
    /// Index of the memory map this switch state implies, for
    /// <see cref="Memory.SetMemoryConfiguration"/>. Bank switching is a configuration swap rather
    /// than a re-map of 12 KB of handlers, so a switch access costs the same as a pointer
    /// assignment — which matters because some software toggles banks in tight loops.
    /// </summary>
    public int MemoryConfiguration =>
        (ReadRam ? 4 : 0) | (Bank1Selected ? 2 : 0) | (WriteEnabled ? 1 : 0);

    /// <summary>Raised when an access changed the memory map, with the new configuration index.</summary>
    public event Action<int>? MemoryConfigurationChanged;

    /// <summary>
    /// Applies the side effect of an access to $C080-$C08F. Reads and writes select the same things,
    /// but only a read arms the pre-write flip-flop.
    /// </summary>
    public void Access(ushort address, bool isRead)
    {
        var previousConfiguration = MemoryConfiguration;

        // Bit 3 picks the bank, bits 1-0 pick the read source and whether writes are being asked
        // for. Bit 2 is not decoded, which is why $C084-$C087 mirror $C080-$C083.
        Bank1Selected = (address & 0x08) != 0;

        var lowBits = address & 0x03;
        ReadRam = lowBits == 0x00 || lowBits == 0x03;

        var oddAddress = (address & 0x01) != 0;
        if (!oddAddress)
        {
            // An even address write-protects the card immediately, and disarms any half-finished
            // enable sequence with it.
            WriteEnabled = false;
        }
        else if (PreWrite)
        {
            // Second consecutive access to an odd address: the sequence completes.
            WriteEnabled = true;
        }

        // Only a read arms it. A write to the switch leaves the card protected however many times
        // it is repeated.
        PreWrite = oddAddress && isRead;

        var configuration = MemoryConfiguration;
        if (configuration != previousConfiguration)
            MemoryConfigurationChanged?.Invoke(configuration);
    }

    /// <summary>
    /// Returns the card to its power-on state: ROM visible, writes protected, bank 2 selected. The
    /// card's contents are deliberately kept — a reset does not clear RAM, and software that parks
    /// code in the card across a reset depends on that.
    /// </summary>
    public void Reset()
    {
        var previousConfiguration = MemoryConfiguration;

        ReadRam = false;
        Bank1Selected = false;
        WriteEnabled = false;
        PreWrite = false;

        if (MemoryConfiguration != previousConfiguration)
            MemoryConfigurationChanged?.Invoke(MemoryConfiguration);
    }

    // --- Snapshot support (consumed by the apple2-languagecard snapshot module) ---

    internal (bool ReadRam, bool Bank1Selected, bool WriteEnabled, bool PreWrite) GetSnapshotState()
        => (ReadRam, Bank1Selected, WriteEnabled, PreWrite);

    /// <summary>
    /// Restores the switch state. Does not raise <see cref="MemoryConfigurationChanged"/> — the
    /// caller applies the configuration itself, so a restore cannot half-apply if the event has no
    /// subscriber yet.
    /// </summary>
    internal void RestoreSnapshotState(bool readRam, bool bank1Selected, bool writeEnabled, bool preWrite)
    {
        ReadRam = readRam;
        Bank1Selected = bank1Selected;
        WriteEnabled = writeEnabled;
        PreWrite = preWrite;
    }
}
