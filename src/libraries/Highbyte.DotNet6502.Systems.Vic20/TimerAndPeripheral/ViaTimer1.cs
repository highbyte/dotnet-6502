namespace Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;

/// <summary>
/// VIA 6522 Timer 1 — 16-bit countdown, modelled after the C64's CiaTimer but with the
/// VIA-specific behaviour differences:
///
/// - Writing T1L-H ($9117 / $9127) both loads the counter from the latch AND starts
///   the timer (CIA requires a separate start-bit write in the Control Register).
/// - Free-running (continuous) mode is the default; one-shot is selected by ACR bit 6.
/// - Reading T1C-H ($9115 / $9125) automatically clears the IFR Timer 1 flag.
///
/// Timer 2 is not needed for basic VIC-20 keyboard and cursor operation so only
/// Timer 1 is implemented here.
/// </summary>
public class ViaTimer1
{
    private readonly Vic20 _vic20;
    private readonly ViaIRQ _irq;

    private ushort _latch;
    private ushort _counter;
    private bool   _running;
    private bool   _freeRunning = false;  // ACR bit 6: 1 = free-run (VIA ACR resets to $00 = one-shot)

    public ushort Counter => _counter;
    public ushort Latch   => _latch;

    public ViaTimer1(Vic20 vic20, ViaIRQ irq)
    {
        _vic20 = vic20;
        _irq   = irq;
    }

    public void SetFreeRunning(bool freeRunning) => _freeRunning = freeRunning;

    // Write to T1L-L ($9116) — latch low only, no effect on running counter.
    public void WriteLatchLo(byte value)
    {
        _latch = (ushort)((_latch & 0xFF00) | value);
    }

    // Write to T1L-H ($9117) — latch high only, does NOT start timer (VIA 6522 spec).
    public void WriteLatchHiOnly(byte value)
    {
        _latch = (ushort)((_latch & 0x00FF) | ((ushort)value << 8));
    }

    // Write to T1C-H ($9115) — loads counter from latch, starts timer, clears IFR (VIA 6522 spec).
    public void WriteCounterHi(byte value)
    {
        _latch   = (ushort)((_latch & 0x00FF) | ((ushort)value << 8));
        _counter = _latch;
        _irq.ClearTimer1Condition();
        _running = true;
    }

    // Read T1C-H — clears the IFR Timer 1 flag as a side-effect (VIA behaviour).
    public byte ReadCounterHi()
    {
        _irq.ClearTimer1Condition();
        return (byte)(_counter >> 8);
    }

    public byte ReadCounterLo() => (byte)(_counter & 0xFF);
    public byte ReadLatchLo()   => (byte)(_latch   & 0xFF);
    public byte ReadLatchHi()   => (byte)(_latch   >> 8);

    public void ProcessTimer(ulong cyclesExecuted)
    {
        if (!_running) return;

        if (_counter >= cyclesExecuted)
            _counter -= (ushort)cyclesExecuted;
        else
            _counter = 0;

        if (_counter == 0)
        {
            _irq.SetTimer1Condition();

            if (_irq.IsTimer1Enabled)
                _irq.Trigger(_vic20.CPU, "VIA_T1");

            if (_freeRunning)
            {
                // Reload from latch and keep running — IFR flag cleared on reload.
                _counter = _latch;
                _irq.ClearTimer1Condition();
            }
            else
            {
                _running = false;
            }
        }
    }
}
