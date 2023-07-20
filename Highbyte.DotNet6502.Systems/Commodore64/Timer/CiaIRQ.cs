namespace Highbyte.DotNet6502.Systems.Commodore64.Timer;

public class CiaIRQ
{

    private readonly Dictionary<IRQSource, bool> _latches = new();

    public CiaIRQ()
    {
        foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
        {
            _latches.Add(source, false);
        }
    }

    public void Raise(IRQSource source, CPU cpu)
    {
        _latches[source] = true;
        cpu.IRQ = true;
    }

    public bool IsLatched(IRQSource source)
    {
        return _latches[source];
    }

    public void SetLatch(IRQSource source)
    {
        _latches[source] = true;
    }
    public void ClearLatch(IRQSource source)
    {
        _latches[source] = false;
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
