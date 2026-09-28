namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// A CIA's interrupt sources: which are enabled (the mask written to the interrupt control
/// register) and which have their flag set (what a read of it returns). Both are bitmasks with
/// one bit per source, at the source's bit position in the register (<see cref="IRQSource"/>),
/// the same representation as the CPU's <see cref="CPUInterrupts"/>, so the timers' checks are
/// integer tests and the register read is the mask itself.
/// </summary>
public class CiaIRQ
{
    // Indexed by IRQSource enum value, which is the corresponding bit position in the CIA interrupt control register.
    // Bits 5 and 6 are unused in the CIA register, so those entries are intentionally empty and should never be used.
    private static readonly string[] s_interruptSourceNames =
    [
        "CIA.TimerA",
        "CIA.TimerB",
        "CIA.TimeOfDayAlarm",
        "CIA.SerialShiftRegister",
        "CIA.FlagLine",
        "",
        "",
        "CIA.Any",
    ];

    /// <summary>The sources a device can raise: bits 0-4 of the register (bit 7 is the interrupt bit).</summary>
    private const byte SourceMask = 0b0001_1111;

    private readonly bool _useNMI;
    private byte _enabledMask;
    private byte _conditionMask;

    // The CPU's handles for the source names, looked up once per CPU instance so that raising
    // and releasing the lines is a mask operation rather than a name lookup.
    private CPUInterrupts? _handlesFor;
    private readonly InterruptSource[] _handles = new InterruptSource[SourceMask + 1];

    public CiaIRQ(bool useNMI)
    {
        _useNMI = useNMI;
    }

    private static byte Bit(IRQSource source) => (byte)(1 << (int)source);

    /// <summary>
    /// The flags as a read of the interrupt control register returns them: bits 0-4 for the
    /// sources whose condition is set, bit 7 when the chip has driven its interrupt output.
    /// </summary>
    public byte Flags => _conditionMask;

    /// <summary>True if any enabled source has its flag set.</summary>
    public bool AnyEnabledFlagSet => (_conditionMask & _enabledMask & SourceMask) != 0;

    /// <summary>
    /// Assert the chip's interrupt output for a source. Without a cycle the CPU takes the interrupt
    /// at its next instruction boundary.
    /// </summary>
    public void Trigger(IRQSource source, CPU cpu) => Trigger(source, cpu, atBusCycle: 0);

    /// <summary>
    /// Assert the chip's interrupt output for a source that arose during the given bus cycle; the
    /// CPU applies its end-of-instruction sampling rule to that cycle.
    /// </summary>
    public void Trigger(IRQSource source, CPU cpu, ulong atBusCycle)
    {
        _conditionMask |= Bit(IRQSource.Any);
        var handle = Handle(source, cpu.CPUInterrupts);

        if (_useNMI)
        {
            // Raise NMI (Non-Maskable Interrupt)
            cpu.CPUInterrupts.SetNMIActive(handle, atBusCycle);
        }
        else
        {
            // Raise IRQ (Interrupt Request). The 6526 holds its interrupt output until the
            // interrupt control register is read (Acknowledge): servicing the interrupt does
            // not release it, so a handler that returns without reading the register is
            // re-entered at once, and an interrupt whose entry sequence an NMI hijacked is
            // still pending when the NMI handler returns.
            cpu.CPUInterrupts.SetIRQActive(handle, autoAcknowledge: false, atBusCycle);
        }
    }

    public static string GetInterruptSourceName(IRQSource source)
        => s_interruptSourceNames[(int)source];

    public bool IsEnabled(IRQSource source) => (_enabledMask & Bit(source)) != 0;

    public void Enable(IRQSource source) => _enabledMask |= Bit(source);

    public void Disable(IRQSource source) => _enabledMask &= (byte)~Bit(source);

    public bool IsConditionSet(IRQSource source) => (_conditionMask & Bit(source)) != 0;

    public void ConditionSet(IRQSource source) => _conditionMask |= Bit(source);

    public void ConditionClear(IRQSource source) => _conditionMask &= (byte)~Bit(source);

    public void ConditionClearAll() => _conditionMask = 0;

    /// <summary>Release the chip's interrupt output for every source.</summary>
    public void Acknowledge(CPU cpu) => Acknowledge(cpu, releasedAtBusCycle: 0);

    /// <summary>
    /// Release the chip's interrupt output for every source, during the given bus cycle; the CPU
    /// still takes an IRQ it sampled active before that cycle.
    /// </summary>
    public void Acknowledge(CPU cpu, ulong releasedAtBusCycle)
    {
        var interrupts = cpu.CPUInterrupts;
        for (var bit = 0; bit <= (int)IRQSource.FlagLine; bit++)
        {
            var handle = Handle((IRQSource)bit, interrupts);
            if (_useNMI)
                interrupts.SetNMIInactive(handle);
            else
                interrupts.SetIRQInactive(handle, releasedAtBusCycle);
        }
    }

    private InterruptSource Handle(IRQSource source, CPUInterrupts interrupts)
    {
        if (!ReferenceEquals(_handlesFor, interrupts))
        {
            for (var bit = 0; bit <= (int)IRQSource.FlagLine; bit++)
                _handles[bit] = interrupts.GetSource(s_interruptSourceNames[bit]);
            _handlesFor = interrupts;
        }
        return _handles[(int)source];
    }
}

/// <summary>
/// CIA IRQ source flags in CIA 1 & 2 IRQ registers (0xdc0d, 0xdd0d).
/// The enum values represents the bit position of the flag in the register.
/// Ref: https://github.com/mist64/c64ref/blob/master/Source/c64io/c64io_mapc64.txt
/// </summary>
public enum IRQSource
{
    TimerA = 0,
    TimerB = 1,
    TimeOfDayAlarm = 2,
    SerialShiftRegister = 3,
    FlagLine = 4,
    Any = 7
}
