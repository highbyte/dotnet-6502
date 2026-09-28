namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// The VIC-II's interrupt sources: which are enabled (the interrupt enable register, $D01A) and
/// which are latched (the interrupt register, $D019). Both are bitmasks with one bit per source,
/// at the source's bit position in the registers (<see cref="IRQSource"/>), the same
/// representation as the CPU's <see cref="CPUInterrupts"/>. The IRQ line is driven while an
/// enabled source is latched; each source is a separate, manually released line at the CPU.
/// </summary>
public class Vic2IRQ
{
    // Indexed by IRQSource enum value, which is the corresponding bit position in the VIC-II interrupt register.
    // Bits 4, 5 and 6 are unused in the VIC-II register, so those entries are intentionally empty and should never be used.
    private static readonly string[] s_interruptSourceNames =
    [
        "VIC2.RasterCompare",
        "VIC2.SpriteToBackgroundCollision",
        "VIC2.SpriteToSpriteCollision",
        "VIC2.LightPenTrigger",
        "",
        "",
        "",
        "VIC2.Any",
    ];

    /// <summary>The sources: bits 0-3 of the registers (bit 7 of $D019 is the interrupt output).</summary>
    public const byte SourceMask = 0b0000_1111;

    // ConfiguredIRQRasterLine = null means not configured yet, and raster IRQ should occur when raster line wraps around from it's max (defined by C64/VIC2 model) to 0.
    public ushort? ConfiguredIRQRasterLine { get; set; } = null;

    private byte _enabledMask;
    private byte _latchedMask;

    // The CPU's handles for the source names, looked up once per CPU instance so that raising
    // and releasing the lines is a mask operation rather than a name lookup.
    private CPUInterrupts? _handlesFor;
    private readonly InterruptSource[] _handles = new InterruptSource[SourceMask + 1];

    private static byte Bit(IRQSource source) => (byte)(1 << (int)source);

    /// <summary>The enabled sources, as bits 0-3 of the interrupt enable register.</summary>
    public byte EnabledMask => _enabledMask;

    /// <summary>The latched sources, as bits 0-3 of the interrupt register.</summary>
    public byte LatchedMask => _latchedMask;

    /// <summary>True while the chip drives its interrupt output: an enabled source is latched.</summary>
    public bool OutputActive => (_enabledMask & _latchedMask) != 0;

    public bool IsEnabled(IRQSource source) => (_enabledMask & Bit(source)) != 0;

    public bool IsTriggered(IRQSource source) => (_latchedMask & Bit(source)) != 0;

    /// <summary>
    /// Enable a source. If it is already latched the IRQ line goes active now, on the cycle of the
    /// register write that enabled it.
    /// </summary>
    public void Enable(IRQSource source, CPU cpu)
    {
        _enabledMask |= Bit(source);
        if (IsTriggered(source))
            cpu.CPUInterrupts.SetIRQActive(Handle(source, cpu.CPUInterrupts), autoAcknowledge: false, cpu.BusCycles);
    }

    /// <summary>
    /// Disable a source; the IRQ line is released on the cycle of the register write. The CPU
    /// still takes an interrupt it sampled active before that cycle.
    /// </summary>
    public void Disable(IRQSource source, CPU cpu)
    {
        _enabledMask &= (byte)~Bit(source);
        cpu.CPUInterrupts.SetIRQInactive(Handle(source, cpu.CPUInterrupts), cpu.BusCycles);
    }

    // --- Snapshot support ---
    // Sets the enable/trigger flags directly without touching the CPU interrupt line. The CPU's
    // IRQ source state is restored separately by the cpu-6502 snapshot module, so re-raising it
    // here (as Enable/Trigger would) is unnecessary and would risk double-restoring it.
    internal void RestoreSnapshotState(IRQSource source, bool enabled, bool triggered)
    {
        if (enabled)
            _enabledMask |= Bit(source);
        else
            _enabledMask &= (byte)~Bit(source);
        if (triggered)
            _latchedMask |= Bit(source);
        else
            _latchedMask &= (byte)~Bit(source);
    }

    /// <summary>
    /// Latch a source; asserts the IRQ line if the source is enabled. Without a cycle the CPU takes
    /// the interrupt at its next instruction boundary.
    /// </summary>
    public void Trigger(IRQSource source, CPU cpu) => Trigger(source, cpu, atBusCycle: 0);

    /// <summary>
    /// Latch a source that arose during the given bus cycle; the CPU applies its end-of-instruction
    /// sampling rule to that cycle (see <see cref="CPUInterrupts.SetIRQSourceActive(string, bool, ulong)"/>).
    /// </summary>
    public void Trigger(IRQSource source, CPU cpu, ulong atBusCycle)
    {
        _latchedMask |= Bit(source);
        if (IsEnabled(source))
            cpu.CPUInterrupts.SetIRQActive(Handle(source, cpu.CPUInterrupts), autoAcknowledge: false, atBusCycle);
    }

    /// <summary>
    /// Clear a latched source (an interrupt register write); the IRQ line is released on the cycle
    /// of the write. The CPU still takes an interrupt it sampled active before that cycle, so an
    /// acknowledge in an instruction's last cycle does not stop the interrupt.
    /// </summary>
    public void ClearTrigger(IRQSource source, CPU cpu)
    {
        _latchedMask &= (byte)~Bit(source);
        cpu.CPUInterrupts.SetIRQInactive(Handle(source, cpu.CPUInterrupts), cpu.BusCycles);
    }

    public static string GetInterruptSourceName(IRQSource source)
        => s_interruptSourceNames[(int)source];

    private InterruptSource Handle(IRQSource source, CPUInterrupts interrupts)
    {
        if (!ReferenceEquals(_handlesFor, interrupts))
        {
            for (var bit = 0; bit <= (int)IRQSource.LightPenTrigger; bit++)
                _handles[bit] = interrupts.GetSource(s_interruptSourceNames[bit]);
            _handlesFor = interrupts;
        }
        return _handles[(int)source];
    }
}

/// <summary>
/// VIC-II IRQ source flags in IRQ register (0xd019).
/// The enum values represents the bit position of the flag in the register.
/// Ref: https://github.com/mist64/c64ref/blob/master/Source/c64io/c64io_mapc64.txt#L1202
/// </summary>
/// <summary>
/// The VIC-II interrupt sources, valued by their bit in the interrupt and interrupt-enable
/// registers ($D019 and $D01A): raster compare (IRST), sprite-to-background collision (IMBC),
/// sprite-to-sprite collision (IMMC) and light pen (ILP); bit 7 is the interrupt output.
/// </summary>
public enum IRQSource
{
    RasterCompare = 0,
    SpriteToBackgroundCollision = 1,
    SpriteToSpriteCollision = 2,
    LightPenTrigger = 3,
    Any = 7
}
