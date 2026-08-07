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
/// <para><b>Known limitation.</b> Booting the System Master takes ~35 emulated seconds, most of it
/// DOS's own one-second motor spin-up wait, entered 31 times: RWTS decides the drive
/// is stopped by comparing successive reads of the data register, and that decision depends on
/// real read timing this model does not reproduce. Everything loads correctly, just slower than
/// a real machine (~7 s). Nibblizer sync-gap sizes were swept (20/5, 16/16, 12/12, 10/10, 9/9)
/// with no effect on the count, so the cause is the timing model rather than the track layout.
/// Removing the wait needs a cycle-accurate read path — the natural companion to sequencer-PROM
/// emulation, if copy-protected media is ever supported.</para>
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
    private ulong _motorSwitchedOffAtCycle;
    private int _selectedDrive = 1;
    private bool _q6;
    private bool _q7;
    private int _nibblePosition;

    private byte[][]? _nibbleTracks;

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
        || _cpuCycleProvider() - _motorSwitchedOffAtCycle < SpinDownCycles;

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
        _nibbleTracks = Disk2TrackNibblizer.BuildNibbleTracks(diskImageData);
    }

    public void RemoveDiskImage() => _nibbleTracks = null;

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
        _motorSwitchedOffAtCycle = 0;
        _selectedDrive = 1;
        _q6 = false;
        _q7 = false;
        // Head position is intentionally kept — a reset does not move a physical head.
    }
}
