using Highbyte.DotNet6502.Systems.Apple2.DiskImage;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;

namespace Highbyte.DotNet6502.Systems.Apple2.Disk2;

/// <summary>
/// The Disk II controller card in slot 6, emulated at the soft-switch level (read-only).
///
/// The card has no CPU and no protocol — the Apple's own 6502 steps the head by toggling four
/// stepper phase magnets, spins the motor, and polls raw disk bytes out of a shift register, all
/// through 16 soft switches at $C0E0-$C0EF. RWTS and custom game loaders run unmodified on the
/// emulated CPU against the nibble streams produced by <see cref="Disk2TrackNibblizer"/>.
///
/// Timing model: every read of the data register delivers the next nibble of the track stream.
/// The consumer's own polling paces the data, so a reader can never miss a byte no matter how
/// slowly it collects them — the property that makes this robust without a cycle-accurate bus,
/// which this machine does not have. Two alternatives were implemented and measured against a
/// real DOS 3.3 System Master boot, and both are worse:
/// <list type="bullet">
/// <item>Deriving the head position purely from elapsed CPU cycles (a true rotational model):
/// the boot ROM stage ran and stepped the head, but DOS's sector reads never completed.</item>
/// <item>Holding the latch for N cycles so that reads closer together than N return the same
/// byte (any N from 12 to 17 tried): the boot never reached the DOS banner at all.</item>
/// </list>
///
/// <para><b>Known limitation.</b> Booting the System Master reaches the DOS banner in ~35 emulated
/// seconds, and with a language card fitted (the default) the drive keeps loading Integer BASIC
/// for another minute — ~95 s before it settles, against ~49 s on a 48 KB machine and ~7 s on real
/// hardware. Most of it is DOS's own one-second motor spin-up wait, entered repeatedly: RWTS
/// decides the drive is stopped by comparing successive reads of the data register, and that
/// decision depends on real read timing this model does not reproduce. Everything loads correctly,
/// just slowly. Nibblizer sync-gap sizes were swept (20/5, 16/16, 12/12, 10/10, 9/9) with no
/// effect, so the cause is the timing model rather than the track layout. Removing the wait needs
/// a cycle-accurate read path — the natural companion to sequencer-PROM emulation, if
/// copy-protected media is ever supported.</para>
///
/// The motor's ~1 second spin-down is modeled: the card's one-shot keeps the disk turning after
/// the motor-off switch, so an access shortly after a stop still finds data under the head
/// rather than a dead stream.
///
/// Simplifications (fine for standard 16-sector software, documented deviations from hardware):
/// write mode is a no-op (the disk is always write-protected), drive 2 is not present, and the
/// sequencer PROM's bit-level behavior and true rotational timing (needed only by
/// copy-protection schemes) are not modeled.
/// </summary>
public class Disk2Controller
{
    /// <summary>The peripheral slot the controller occupies.</summary>
    public const int Slot = 6;

    /// <summary>First soft-switch address: $C080 + slot × $10.</summary>
    public const ushort IoBaseAddress = 0xC0E0;

    /// <summary>Where the 256-byte P5 boot ROM appears: $C600 for slot 6.</summary>
    public const ushort BootRomBaseAddress = 0xC600;

    public const int BootRomSize = 256;

    /// <summary>Highest half-track the head can step to (track 34).</summary>
    public const int MaxHalfTrack = (DskParser.Tracks - 1) * 2;

    /// <summary>
    /// Value read from a register access that carries no data (the high bit is clear, so
    /// polling loops keep waiting).
    /// </summary>
    private const byte NoDataLatched = 0x00;

    private const byte NoDataValue = 0xFF;

    /// <summary>
    /// How long the drive keeps turning after the motor-off switch — the card's one-shot,
    /// about a second at 1 MHz.
    /// </summary>
    public const ulong SpinDownCycles = 1_020_484;


    private readonly Func<ulong> _cpuCycleProvider;

    private readonly bool[] _phaseOn = new bool[4];
    private int _halfTrack;
    private bool _motorSwitchedOn;

    /// <summary>
    /// When the motor switch was last turned off, or null if the drive has not been switched off
    /// since it was last stopped for good. Null rather than 0, for the same reason the game port's
    /// trigger stamp is nullable: at power-on the CPU cycle counter is 0 too, so treating "never
    /// switched off" as "switched off at cycle 0" puts the one-shot inside its own spin-down window
    /// and a drive that has never run reports as spinning for its first million cycles.
    /// </summary>
    private ulong? _motorSwitchedOffAtCycle;
    private int _selectedDrive = 1;
    private bool _q6;
    private bool _q7;
    private int _nibblePosition;

    private byte[][]? _nibbleTracks;

    /// <summary>
    /// The inserted image exactly as supplied. Kept alongside the nibblized tracks because
    /// nibblizing is one-way: a snapshot has to embed the original image to be able to re-insert
    /// the same disk on restore.
    /// </summary>
    private byte[]? _rawDiskImageData;

    /// <param name="cpuCycleProvider">
    /// Source of the CPU's cumulative cycle count, used only to time the motor's spin-down.
    /// Defaults to a stopped clock, which makes spin-down expire immediately.
    /// </param>
    public Disk2Controller(Func<ulong>? cpuCycleProvider = null)
    {
        _cpuCycleProvider = cpuCycleProvider ?? (static () => 0);
    }

    /// <summary>The P5 (341-0027) 16-sector boot ROM image, when configured.</summary>
    public byte[]? BootRom { get; private set; }

    public bool IsDiskInserted => _nibbleTracks != null;

    /// <summary>Read-only emulation: any inserted disk reports as write-protected.</summary>
    public bool IsWriteProtected => true;

    /// <summary>State of the motor soft switch ($C0E9 on / $C0E8 off).</summary>
    public bool IsMotorOn => _motorSwitchedOn;

    /// <summary>
    /// Whether the disk is actually turning: the switch is on, or it was switched off less
    /// than <see cref="SpinDownCycles"/> ago and the one-shot still holds the motor.
    /// </summary>
    public bool IsSpinning => _motorSwitchedOn
        || (_motorSwitchedOffAtCycle is { } switchedOffAt
            && _cpuCycleProvider() - switchedOffAt < SpinDownCycles);

    /// <summary>The whole track currently under the head.</summary>
    public int CurrentTrack => _halfTrack / 2;

    public int SelectedDrive => _selectedDrive;

    /// <summary>
    /// Total data nibbles delivered. A host can watch this (with <see cref="IsMotorOn"/>) to
    /// drive a disk-activity indicator.
    /// </summary>
    public ulong DataReadCount { get; private set; }

    /// <summary>
    /// Whether the controller responds on the bus: both the boot ROM and a disk must be present.
    /// Keeping the boot ROM invisible until a disk is inserted makes the Autostart ROM's slot
    /// scan fall through to BASIC instead of hanging on an empty drive (a deliberate usability
    /// deviation from real hardware).
    /// </summary>
    public bool IsEnabled => BootRom != null && IsDiskInserted;

    public void SetBootRom(byte[] bootRom)
    {
        ArgumentNullException.ThrowIfNull(bootRom);
        if (bootRom.Length != BootRomSize)
            throw new DotNet6502Exception(
                $"Disk II boot ROM image must be {BootRomSize} bytes, got {bootRom.Length}.");
        BootRom = bootRom;
    }

    /// <summary>Inserts a DOS-ordered 140 KB disk image, nibblizing it for the read head.</summary>
    /// <exception cref="InvalidDataException">The image is not a 140 KB DOS-ordered image.</exception>
    public void InsertDiskImage(byte[] diskImageData)
    {
        // Detected from the image's own contents rather than its file name: extensions are not
        // reliable in the wild (archive.org's "Dangerous Dave in the Deserted Pirate's Hideout" is
        // named .dsk and is ProDOS-ordered), and nibblizing with the wrong order produces a track
        // of plausible garbage rather than an error — which presents as a drive fault, not a
        // misread image.
        SectorOrder = DiskSectorOrderDetector.Detect(diskImageData);
        _nibbleTracks = Disk2TrackNibblizer.BuildNibbleTracks(
            diskImageData, Disk2TrackNibblizer.DefaultVolume, SectorOrder);
        _rawDiskImageData = diskImageData;
    }

    public void RemoveDiskImage()
    {
        _nibbleTracks = null;
        _rawDiskImageData = null;
        SectorOrder = DiskSectorOrder.Dos;
    }

    /// <summary>
    /// The sector order detected for the inserted image. Exposed because it is the first thing to
    /// check when a disk boots on other emulators but not here.
    /// </summary>
    public DiskSectorOrder SectorOrder { get; private set; } = DiskSectorOrder.Dos;

    /// <summary>Value the CPU sees at a boot ROM address ($C600-$C6FF).</summary>
    public byte ReadBootRom(ushort address)
    {
        if (!IsEnabled)
            return Apple2SoftSwitches.UnconnectedReadValue;
        return BootRom![address & 0xFF];
    }

    /// <summary>
    /// Applies the side effect of a soft-switch access ($C0E0-$C0EF) and returns the value the
    /// CPU reads. Like the rest of the Apple II I/O page, writes trigger the same side effects.
    /// </summary>
    public byte BusAccess(ushort address)
    {
        switch (address & 0x0F)
        {
            case 0x0:
            case 0x1:
            case 0x2:
            case 0x3:
            case 0x4:
            case 0x5:
            case 0x6:
            case 0x7:
                SetPhase((address >> 1) & 0x03, on: (address & 0x01) != 0);
                return Apple2SoftSwitches.UnconnectedReadValue;

            case 0x8:
                if (_motorSwitchedOn)
                {
                    _motorSwitchedOn = false;
                    _motorSwitchedOffAtCycle = _cpuCycleProvider();   // start the one-shot
                }
                return Apple2SoftSwitches.UnconnectedReadValue;
            case 0x9:
                _motorSwitchedOn = true;
                return Apple2SoftSwitches.UnconnectedReadValue;

            case 0xA:
                _selectedDrive = 1;
                return Apple2SoftSwitches.UnconnectedReadValue;
            case 0xB:
                _selectedDrive = 2;
                return Apple2SoftSwitches.UnconnectedReadValue;

            case 0xC:   // Q6L: in read mode, the data shift register
                _q6 = false;
                return _q7 ? NoDataLatched : ReadDataNibble();
            case 0xD:   // Q6H: first half of the write-protect sense sequence
                _q6 = true;
                return NoDataLatched;
            case 0xE:   // Q7L: read mode; with Q6 set returns write-protect status in bit 7
                _q7 = false;
                return _q6 && IsWriteProtected ? (byte)0x80 : NoDataLatched;
            default:    // 0xF, Q7H: write mode (read-only emulation: writes go nowhere)
                _q7 = true;
                return NoDataLatched;
        }
    }

    /// <summary>
    /// One phase magnet turning on pulls the head one half-track toward it when it is adjacent
    /// to the head's current position. Phase alignment repeats every four half-tracks, which is
    /// how RWTS's two-pulses-per-track seek and the boot ROM's 80-pulse recalibration slam to
    /// track 0 both come out right.
    /// </summary>
    private void SetPhase(int phase, bool on)
    {
        _phaseOn[phase] = on;
        if (!on)
            return;

        var currentPhase = _halfTrack & 0x03;
        if (phase == ((currentPhase + 1) & 0x03))
            _halfTrack = Math.Min(_halfTrack + 1, MaxHalfTrack);
        else if (phase == ((currentPhase + 3) & 0x03))
            _halfTrack = Math.Max(_halfTrack - 1, 0);
    }

    private byte ReadDataNibble()
    {
        if (_nibbleTracks == null || !IsSpinning || _selectedDrive != 1)
            return NoDataValue;

        DataReadCount++;
        var trackData = _nibbleTracks[CurrentTrack];
        var value = trackData[_nibblePosition % trackData.Length];
        _nibblePosition = (_nibblePosition + 1) % trackData.Length;
        return value;
    }

    public void Reset()
    {
        Array.Clear(_phaseOn);
        _motorSwitchedOn = false;
        _motorSwitchedOffAtCycle = null;
        _selectedDrive = 1;
        _q6 = false;
        _q7 = false;
        // Head position is intentionally kept — a reset does not move a physical head.
    }

    // --- Snapshot support (consumed by the apple2-disk2 snapshot module in the same assembly) ---

    /// <summary>
    /// The inserted image as originally supplied, or null when the drive is empty. The snapshot
    /// embeds these bytes so the same disk can be re-inserted on restore.
    /// </summary>
    internal byte[]? SnapshotRawDiskImageData => _rawDiskImageData;

    /// <summary>
    /// The mechanical and sequencer state that is not derivable from anything else: where the head
    /// is, whether the motor is running (and if coasting, since when), which drive is selected, the
    /// Q6/Q7 latches, and how far into the current track's nibble stream the read head sits.
    ///
    /// <para>The nibble position is the one that is easy to dismiss and should not be: a snapshot
    /// taken during a sector read resumes mid-stream, and starting that stream over would hand the
    /// running RWTS a field header where it expects data.</para>
    /// </summary>
    internal (int HalfTrack, bool MotorOn, ulong? MotorSwitchedOffAtCycle, int SelectedDrive, bool Q6, bool Q7, int NibblePosition)
        GetSnapshotState()
        => (_halfTrack, _motorSwitchedOn, _motorSwitchedOffAtCycle, _selectedDrive, _q6, _q7, _nibblePosition);

    internal void RestoreSnapshotState(
        int halfTrack, bool motorOn, ulong? motorSwitchedOffAtCycle, int selectedDrive, bool q6, bool q7, int nibblePosition)
    {
        _halfTrack = Math.Clamp(halfTrack, 0, MaxHalfTrack);
        _motorSwitchedOn = motorOn;
        _motorSwitchedOffAtCycle = motorSwitchedOffAtCycle;
        _selectedDrive = selectedDrive;
        _q6 = q6;
        _q7 = q7;
        // Guarded rather than trusted: the position indexes into the current track's nibble stream,
        // whose length depends on the nibblizer, so a snapshot written by a different build could
        // otherwise index out of range. ReadDataNibble wraps modulo the track length anyway.
        _nibblePosition = nibblePosition < 0 ? 0 : nibblePosition;
    }
}
