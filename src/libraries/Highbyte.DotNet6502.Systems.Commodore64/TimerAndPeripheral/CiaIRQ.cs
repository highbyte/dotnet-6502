namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

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

    private readonly bool _useNMI;
    private readonly Dictionary<IRQSource, bool> _sourceEnableStatus = new();
    private readonly Dictionary<IRQSource, bool> _sourceConditionStatus = new();

    public CiaIRQ(bool useNMI)
    {
        _useNMI = useNMI;
        foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
        {
            _sourceEnableStatus.Add(source, false);
            _sourceConditionStatus.Add(source, false);
        }
    }

    public void Trigger(IRQSource source, CPU cpu)
    {
        _sourceConditionStatus[IRQSource.Any] = true;
        var interruptSourceName = GetInterruptSourceName(source);

        if (_useNMI)
        {
            // Raise NMI (Non-Maskable Interrupt)
            cpu.CPUInterrupts.SetNMISourceActive(interruptSourceName);
        }
        else
        {
            // Raise IRQ (Interrupt Request)
            cpu.CPUInterrupts.SetIRQSourceActive(interruptSourceName, autoAcknowledge: true);
        }
    }

    public static string GetInterruptSourceName(IRQSource source)
        => s_interruptSourceNames[(int)source];

    public bool IsEnabled(IRQSource source)
    {
        return _sourceEnableStatus[source];
    }
    public void Enable(IRQSource source)
    {
        _sourceEnableStatus[source] = true;
    }
    public void Disable(IRQSource source)
    {
        _sourceEnableStatus[source] = false;
    }

    public bool IsConditionSet(IRQSource source)
    {
        return _sourceConditionStatus[source];
    }
    public void ConditionSet(IRQSource source)
    {
        _sourceConditionStatus[source] = true;
    }

    public void ConditionClear(IRQSource source)
    {
        _sourceConditionStatus[source] = false;
    }
    public void ConditionClearAll()
    {
        foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
        {
            ConditionClear(source);
        }
    }

    public void Acknowledge(CPU cpu)
    {
        foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
        {
            if (source == IRQSource.Any)
                continue;

            var interruptSourceName = GetInterruptSourceName(source);
            if (_useNMI)
                cpu.CPUInterrupts.SetNMISourceInactive(interruptSourceName);
            else
                cpu.CPUInterrupts.SetIRQSourceInactive(interruptSourceName);
        }
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
