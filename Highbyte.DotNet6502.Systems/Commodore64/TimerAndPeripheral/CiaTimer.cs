using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public class CiaTimer
{
    private readonly CiaTimerType _ciaTimerType;
    private readonly IRQSource _iRQSource;
    private readonly C64 _c64;

    // Current 16-bit value of the timer, decremented each cycle when timer is running.
    public ushort InternalTimer { get; private set; } = 0;

    // Latch contains the value was written to timer registers, and is used as start value when timer is started.
    private ushort _internalTimer_Latch = 0;
    public void SetInternalTimer_Latch_HI(byte highbyte) => _internalTimer_Latch.SetHighbyte(highbyte);
    public void SetInternalTimer_Latch_LO(byte lowbyte) => _internalTimer_Latch.SetLowbyte(lowbyte);

    // The contents of the control register for the timer. It's contents is depending on which timer type (CIA1 A/B and CIA2 A/B) it represents.
    private byte _timerControl = 0;
    public byte TimerControl
    {
        get
        {
            return _timerControl;
        }
        set
        {
            int forceLoadTimerBit = _ciaTimerType switch
            {
                CiaTimerType.Cia1_A => (int)Cia1TimerAControl.ForceLoadTimerA,
                CiaTimerType.Cia1_B => (int)Cia1TimerBControl.ForceLoadTimerB,

                // TODO: Implement CIA2 timers
                //CiaTimerType.Cia2_A => (int)Cia2TimerAControl.ForceLoadTimerA,
                //CiaTimerType.Cia2_B => (int)Cia2TimerBControl.ForceLoadTimerB,
                CiaTimerType.Cia2_A => 4,
                CiaTimerType.Cia2_B => 4,
                _ => throw new NotImplementedException()
            };

            // Don't store bit 4 (force latch load), as it's a command (see below)
            var storeValue = value;
            storeValue.ClearBit(forceLoadTimerBit);
            _timerControl = storeValue;

            if (value.IsBitSet(forceLoadTimerBit))
                ResetTimerValue();
        }
    }

    private readonly Stopwatch _realTimer_Stopwatch = new();

    public CiaTimer(CiaTimerType ciaTimerType, IRQSource iRQSource, C64 c64)
    {
        _ciaTimerType = ciaTimerType;
        _iRQSource = iRQSource;
        _c64 = c64;
    }

    public void ProcessTimer(ulong cyclesExecuted)
    {
        var ciaIrq = _c64.Cia.CiaIRQ;

        if (IsTimerStartFlagSet() && _realTimer_Stopwatch.IsRunning)
        {
            var elapsedMs = _realTimer_Stopwatch.ElapsedMilliseconds;
            var startValueMs = CalculateTimerMS(_internalTimer_Latch);
            var remainingMs = startValueMs - elapsedMs;
            if (remainingMs < 0)
                remainingMs = 0;
            InternalTimer = CalculateTimerValue(remainingMs);

            if (InternalTimer == 0)
                ciaIrq.ConditionSet(_iRQSource);

            if (ciaIrq.IsConditionSet(_iRQSource))
            {
                // Timer has reached zero. Trigger interrupt if enabled.
                if (ciaIrq.IsEnabled(_iRQSource))
                    ciaIrq.Trigger(_iRQSource, _c64.CPU);

                // Check if timer should be reloaded from latch. If Timer A RunMode bit is clear, timer should be continously reloaded from latch.
                if (IsTimerRunModeContinious())
                {
                    ResetTimerValue();
                    StartTimer();
                }
                else
                {
                    StopTimer();
                }
            }
        }
    }

    /// <summary>
    /// Returns true if timer should be running.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private bool IsTimerStartFlagSet()
    {
        return _ciaTimerType switch
        {
            CiaTimerType.Cia1_A => TimerControl.IsBitSet((int)Cia1TimerAControl.StartTimerA),
            CiaTimerType.Cia1_B => TimerControl.IsBitSet((int)Cia1TimerBControl.StartTimerB),

            // TODO: Implement CIA2 timers
            //CiaTimerType.Cia2_A => TimerControl.IsBitSet((int)Cia2TimerAControl.StartTimerA),
            //CiaTimerType.Cia2_B => TimerControl.IsBitSet((int)Cia2TimerBControl.StartTimerB),
            CiaTimerType.Cia2_A => false,
            CiaTimerType.Cia2_B => false,
            _ => throw new NotImplementedException()
        };
    }

    /// <summary>
    /// Returns true if timer should be continously be restarted when finished.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private bool IsTimerRunModeContinious()
    {
        // If run mode bit is clear, timer should be continiously reloaded from latch.
        return _ciaTimerType switch
        {
            CiaTimerType.Cia1_A => !TimerControl.IsBitSet((int)Cia1TimerAControl.TimerARunMode),
            CiaTimerType.Cia1_B => !TimerControl.IsBitSet((int)Cia1TimerBControl.TimerBRunMode),

            // TODO: Implement CIA2 timers
            //CiaTimerType.Cia2_A => !TimerControl.IsBitSet((int)Cia2TimerAControl.TimerARunMode),
            //CiaTimerType.Cia2_B => !TimerControl.IsBitSet((int)Cia2TimerBControl.TimerBRunMode),
            CiaTimerType.Cia2_A => false,
            CiaTimerType.Cia2_B => false,

            _ => throw new NotImplementedException()
        };

    }

    private void ResetTimerValue()
    {
        InternalTimer = _internalTimer_Latch;
        StartTimer();
    }

    public void StartTimer()
    {
        _c64.Cia.CiaIRQ.ConditionClear(_iRQSource);
        _realTimer_Stopwatch.Restart();
    }

    private void StopTimer()
    {
        _realTimer_Stopwatch.Stop();
    }

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
        return timerLatchValue / _c64.Model.CPUFrequencyHz * 1000.0;
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
        return (ushort)(milliseconds * _c64.Model.CPUFrequencyHz / 1000.0);
    }
}

/// <summary>
/// Enum values represents each timer in the CIA chips.
/// </summary>
public enum CiaTimerType
{
    Cia1_A,
    Cia1_B,
    Cia2_A,
    Cia2_B
}

/// <summary>
/// Enum values represents bit position in the CIA 1 Timer A Control register.
/// </summary>
public enum Cia1TimerAControl
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
public enum Cia1TimerBControl
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
