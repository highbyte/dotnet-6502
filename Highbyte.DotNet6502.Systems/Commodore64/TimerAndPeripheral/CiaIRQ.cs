namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public class CiaIRQ
{

    private readonly Dictionary<IRQSource, bool> _sourceStatus = new();

    public CiaIRQ()
    {
        foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
        {
            _sourceStatus.Add(source, false);
        }
    }

    public void Raise(IRQSource source, CPU cpu)
    {
        cpu.IRQ = true;
    }

    public bool IsEnabled(IRQSource source)
    {
        return _sourceStatus[source];
    }

    public void Enable(IRQSource source)
    {
        _sourceStatus[source] = true;
    }
    public void Disable(IRQSource source)
    {
        _sourceStatus[source] = false;
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
