namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public class CiaIRQ
{
    private readonly Dictionary<IRQSource, bool> _sourceEnableStatus = new();
    private readonly Dictionary<IRQSource, bool> _sourceConditionStatus = new();

    public CiaIRQ()
    {
        foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
        {
            _sourceEnableStatus.Add(source, false);
            _sourceConditionStatus.Add(source, false);
        }
    }

    public void Trigger(IRQSource source, CPU cpu)
    {
        cpu.CPUInterrupts.SetIRQSourceActive(source.ToString(), autoAcknowledge: true);
    }

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
}

/// <summary>
/// CIA IRQ source flags in CIA IRQ register (0xdc0d).
/// The enum values represents the bit position of the flag in the register.
/// Ref: https://github.com/mist64/c64ref/blob/master/Source/c64io/c64io_mapc64.txt#L2893
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
