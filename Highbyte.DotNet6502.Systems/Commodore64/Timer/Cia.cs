using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems.Commodore64.Timer;

/// <summary>
/// Complex Interface Adapter (CIA) chip.
/// </summary>
public class Cia
{
    private readonly C64 _c64;

    public CiaIRQ CiaIRQ { get; private set; }

    // Current 16-bit value of the timer, decremented each cycle when timer is running.
    private ushort _internalTimer_CIA1_A;
    private ushort _internalTimer_CIA1_B;
    // Latch contains the value was written to timer registers, and is used as start value when timer is started.
    private ushort _internalTimer_CIA1_A_Latch;
    private ushort _internalTimer_CIA1_B_Latch;

    private readonly Stopwatch _realTimer_CIA1_A_Stopwatch = new();
    private readonly Stopwatch _realTimer_CIA1_B_Stopwatch = new();
    private bool _realTimer_CIA1_A_IsRunning = false;
    private bool _realTimer_CIA1_B_IsRunning = false;

    public byte Cia1TimerAHI { get; private set; }
    public byte Cia1TimerALO { get; private set; }
    public byte Cia1TimerBHI { get; private set; }
    public byte Cia1TimerBLO { get; private set; }

    public byte Cia1InteruptControl { get; private set; }

    public byte Cia1TimerAControl { get; private set; }

    public byte Cia1TimerBControl { get; private set; }

    public Cia(C64 c64)
    {
        _c64 = c64;
        CiaIRQ = new CiaIRQ();
    }

    public void ProcessTimers(ulong cyclesExecuted)
    {
        // Timer CIA1 A
        if (Cia1TimerAControl.IsBitSet((int)TimerAControl.StartTimerA) && _realTimer_CIA1_A_Stopwatch.IsRunning)
        {
            // -- VARIANT: Use .NET Stopwatch to count ticks. More accurate than counting cycles.
            // Convert ticks to milliseconds
            var elapsedMs = _realTimer_CIA1_A_Stopwatch.ElapsedMilliseconds;
            var startValueMs = CalculateTimerMS(_internalTimer_CIA1_A_Latch);
            var remainingMs = startValueMs - elapsedMs;
            if (remainingMs < 0)
                remainingMs = 0;
            _internalTimer_CIA1_A = CalculateTimerValue(remainingMs);

            if (_internalTimer_CIA1_A == 0)
            {
                // Timer has reached zero. Raise interrupt if enabled.
                if (CiaIRQ.IsLatched(IRQSource.TimerA))
                    CiaIRQ.Raise(IRQSource.TimerA, _c64.CPU);

                // Check if timer should be reloaded from latch. If Timer A RunMode bit is clear, timer should be continously reloaded from latch.
                if (!Cia1TimerAControl.IsBitSet((int)TimerAControl.TimerARunMode))
                    StartTimer_CIA1_A();
                else
                    StopTimer_CIA1_A();
            }
        }

        // Timer CIA1 B
        if (Cia1TimerBControl.IsBitSet((int)TimerBControl.StartTimerB))
        {
            // TODO
        }
    }

    public void MapIOLocations(Memory mem)
    {
        // CIA #1 Timer A
        mem.MapReader(CiaAddr.CIA1_TIMAHI, Cia1TimerAHILoad);
        mem.MapWriter(CiaAddr.CIA1_TIMAHI, Cia1TimerAHIStore);

        mem.MapReader(CiaAddr.CIA1_TIMALO, Cia1TimerALOLoad);
        mem.MapWriter(CiaAddr.CIA1_TIMALO, Cia1TimerALOStore);

        // CIA #1 Timer B
        mem.MapReader(CiaAddr.CIA1_TIMBHI, Cia1TimerBHILoad);
        mem.MapWriter(CiaAddr.CIA1_TIMBHI, Cia1TimerBHIStore);

        mem.MapReader(CiaAddr.CIA1_TIMBLO, Cia1TimerBLOLoad);
        mem.MapWriter(CiaAddr.CIA1_TIMBLO, Cia1TimerBLOStore);

        // CIA Interrupt Control Register
        mem.MapReader(CiaAddr.CIA1_CIAICR, Cia1InteruptControlLoad);
        mem.MapWriter(CiaAddr.CIA1_CIAICR, Cia1InteruptControlStore);

        // CIA Control Register A
        mem.MapReader(CiaAddr.CIA1_CIACRA, Cia1TimerAControlLoad);
        mem.MapWriter(CiaAddr.CIA1_CIACRA, Cia1TimerAControlStore);

        // CIA Control Register B
        mem.MapReader(CiaAddr.CIA1_CIACRB, Cia1TimerBControlLoad);
        mem.MapWriter(CiaAddr.CIA1_CIACRB, Cia1TimerBControlStore);

    }

    public byte Cia1TimerAHILoad(ushort _) => _internalTimer_CIA1_A.Highbyte();
    public void Cia1TimerAHIStore(ushort _, byte value) => _internalTimer_CIA1_A_Latch.SetHighbyte(value);
    public byte Cia1TimerALOLoad(ushort _) => _internalTimer_CIA1_A.Lowbyte();
    public void Cia1TimerALOStore(ushort _, byte value) => _internalTimer_CIA1_A_Latch.SetLowbyte(value);

    public byte Cia1TimerBHILoad(ushort _) => _internalTimer_CIA1_B.Highbyte();
    public void Cia1TimerBHIStore(ushort _, byte value) => _internalTimer_CIA1_B_Latch.SetHighbyte(value);
    public byte Cia1TimerBLOLoad(ushort _) => _internalTimer_CIA1_B.Lowbyte();
    public void Cia1TimerBLOStore(ushort _, byte value) => _internalTimer_CIA1_B_Latch.SetLowbyte(value);

    public byte Cia1InteruptControlLoad(ushort _)
    {
        // Bits 5-6 are not used, and always returns 0.
        byte value = 0;

        // If timer A has counted down to zero, set bit 0.
        if (_internalTimer_CIA1_A == 0)
            value.SetBit((int)IRQSource.TimerA);

        // If timer B has counted down to zero, set bit 1.
        if (_internalTimer_CIA1_B == 0)
            value.SetBit((int)IRQSource.TimerB);

        // If any IRQ source is set, also set bit 7.
        if (value != 0)
            value.SetBit((int)IRQSource.Any);

        return value;
    }
    public void Cia1InteruptControlStore(ushort _, byte value)
    {
        // Writing to this register enables for disables the different interrupt sources.
        // If bit 7 is set, then other bit also set means to enable that interrupt source.
        // If bit 7 is not set, then other bit also set means to disable that interrupt source.
        // If bits for the specific interupt sources (0-4) are not set, it will not change state.

        if ((value & 0b1000_0000) > 0)
        {
            // Bit 7 is set, enable interrupt sources with bit set
            foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
            {
                if (source == IRQSource.Any)
                    continue;
                if (value.IsBitSet((int)source))
                    CiaIRQ.SetLatch(source);
            }
        }
        else
        {
            // Bit 7 is not set, disable interrupt sources with bit set
            foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
            {
                if (source == IRQSource.Any)
                    continue;
                if (value.IsBitSet((int)source))
                    CiaIRQ.ClearLatch(source);
            }
        }
    }

    public byte Cia1TimerAControlLoad(ushort _) => Cia1TimerAControl;
    public void Cia1TimerAControlStore(ushort _, byte value)
    {
        // Don't store bit 4 (force latch load), as it's a command (see below)
        Cia1TimerAControl = (byte)(value & 0b1110_1111);

        if (value.IsBitSet((int)TimerAControl.ForceLoadTimerA))
            StartTimer_CIA1_A();
    }

    public byte Cia1TimerBControlLoad(ushort _) => Cia1TimerBControl;
    public void Cia1TimerBControlStore(ushort _, byte value)
    {
        // Don't store bit 4 (force latch load), as it's a command (see below)
        Cia1TimerBControl = (byte)(value & 0b1110_1111);

        if (value.IsBitSet((int)TimerBControl.ForceLoadTimerB))
            StartTimer_CIA1_B();
    }

    private void StartTimer_CIA1_A()
    {
        _internalTimer_CIA1_A = _internalTimer_CIA1_A_Latch;
        _realTimer_CIA1_A_Stopwatch.Restart();
    }
    private void StopTimer_CIA1_A()
    {
        _realTimer_CIA1_A_Stopwatch.Stop();
    }

    private void StartTimer_CIA1_B()
    {
    }
    private void StopTimer_CIA1_B()
    {
    }

    //public void PauseAllTimers()
    //{
    //    if (_realTimer_CIA1_A_Stopwatch.IsRunning)
    //    {
    //        _realTimer_CIA1_A_IsRunning = true;
    //        _realTimer_CIA1_A_Stopwatch.Stop();
    //    }
    //    else
    //    {
    //        _realTimer_CIA1_A_IsRunning = false;
    //    }

    //    if (_realTimer_CIA1_B_Stopwatch.IsRunning)
    //    {
    //        _realTimer_CIA1_B_IsRunning = true;
    //        _realTimer_CIA1_B_Stopwatch.Stop();
    //    }
    //    else
    //    {
    //        _realTimer_CIA1_B_IsRunning = false;
    //    }
    //}

    //public void ResumeAllTimers()
    //{
    //    if (_realTimer_CIA1_A_IsRunning)
    //        _realTimer_CIA1_A_Stopwatch.Start();

    //    if (_realTimer_CIA1_B_IsRunning)
    //        _realTimer_CIA1_B_Stopwatch.Start();
    //}

    /// <summary>
    /// Calculates the timer interval in milliseconds based on the latch value.
    /// Formula: 
    /// TIME (s) = LATCH VALUE / CLOCK SPEED
    /// TIME (ms) = (LATCH VALUE / CLOCK SPEED) * 1000
    /// </summary>
    /// <param name="timerLatchValue"></param>
    /// <returns></returns>
    private double CalculateTimerMS(ushort timerLatchValue)
    {
        return (timerLatchValue / _c64.Model.CPUFrequencyHz) * 1000.0;
    }

    /// <summary>
    /// Calculates the CIA timer value based on milliseconds. 
    /// Formula: 
    /// TIMER VALUE = (TIME (ms) * CLOCK SPEED) / 1000
    /// </summary>
    /// <param name="timerLatchValue"></param>
    /// <returns></returns>
    private ushort CalculateTimerValue(double milliseconds)
    {
        return (ushort)((milliseconds * _c64.Model.CPUFrequencyHz) / 1000.0);
    }
}

/// <summary>
/// Enum values represents bit position in the Timer A Control register.
/// </summary>
public enum TimerAControl
{
    /// <summary>
    /// 1 = Start, 0 = Stop
    /// </summary>
    StartTimerA = 0,
    /// <summary>
    /// 1=Timer A output appears on Bit 6 of Port B
    /// </summary>
    SelectTimerAOnPortBOutput = 1,
    /// <summary>
    /// 1=toggle Bit 6, 0=pulse Bit 6 for one cycle
    /// </summary>
    PortBOutputMode = 2,
    /// <summary>
    /// 1=one-shot, 0=continuous
    /// </summary>
    TimerARunMode = 3,
    /// <summary>
    /// Force latched value to be loaded to Timer A counter (1=force load strobe)
    /// </summary>
    ForceLoadTimerA = 4,
    /// <summary>
    /// 1=count microprocessor cycles, 0=count signals on CNT line at pin 4 of User Port
    /// </summary>
    TimerAInputMode = 5,
    /// <summary>
    /// Serial Port (56332, $DC0C) mode (1=output, 0=input)
    /// </summary>
    SerialPortMode = 6,
    /// <summary>
    /// Time of Day Clock frequency (1=50 Hz required on TOD pin, 0=60 Hz)
    /// </summary>
    TimeOfDayFrequency = 7
}

/// <summary>
/// Enum values represents bit position in the Timer B Control register.
/// </summary>
public enum TimerBControl
{
    /// <summary>
    /// 1 = Start, 0 = Stop
    /// </summary>
    StartTimerB = 0,
    /// <summary>
    /// 1=Timer B output appears on Bit 7 of Port B
    /// </summary>
    SelectTimerBOnPortBOutput = 1,
    /// <summary>
    /// 1=toggle Bit 7, 0=pulse Bit 7 for one cycle
    /// </summary>
    PortBOutputMode = 2,
    /// <summary>
    /// 1=one-shot, 0=continuous
    /// </summary>
    TimerBRunMode = 3,
    /// <summary>
    /// Force latched value to be loaded to Timer B counter (1=force load strobe)
    /// </summary>
    ForceLoadTimerB = 4,
    /// <summary>
    /// Bits 5-6: Timer B input mode
    /// 00 = Timer B counts microprocessor cycles
    /// 01 = Count signals on CNT line at pin 4 of User Port
    /// 10 = Count each time that Timer A counts down to 0
    /// 11 = Count Timer A 0's when CNT pulses are also present
    /// </summary>
    TimerBInputMode0 = 5,
    TimerBInputMode1 = 6,

    /// <summary>
    /// Select Time of Day write (0=writing to TOD registers sets alarm, 1=writing to TOD registers sets clock)
    /// </summary>
    SelectTimeOfDayWrite = 7
}
