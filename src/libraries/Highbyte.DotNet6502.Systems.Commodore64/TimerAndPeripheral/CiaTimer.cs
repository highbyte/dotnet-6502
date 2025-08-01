using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public class CiaTimer
{
    private readonly CiaTimerType _ciaTimerType;
    private readonly IRQSource _iRQSource;
    private readonly C64 _c64;
    private readonly CiaIRQ _ciaIRQ;
    private readonly int _timerControlStartBit;
    private readonly int _timerControlForceLoadBit;
    private readonly int _timerControlRunModeBit;

    // Latch contains the value was written to timer registers, and is used as start value when timer is started.
    private ushort _internalTimer_Latch = 0;
    public void SetInternalTimer_Latch_HI(byte highbyte)
    {
        _internalTimer_Latch.SetHighbyte(highbyte);
        // If timer is not running, set the internal timer to the latch value.
        if (!_timerIsRunning)
        {
            InternalTimer = _internalTimer_Latch;
        }
    }
    public void SetInternalTimer_Latch_LO(byte lowbyte)
    {
        _internalTimer_Latch.SetLowbyte(lowbyte);
        // If timer is not running, set the internal timer to the latch value.
        if (!_timerIsRunning)
        {
            InternalTimer = _internalTimer_Latch;
        }
    }

    // The contents of the control register for the timer. It's contents is depending on which timer type (A/B) it represents.
    private byte _timerControl = 0;
    public byte TimerControl
    {
        get
        {
            return _timerControl;
        }
        set
        {
            // Handle force load bit
            // Don't store bit 4 (force latch load), as it's a command (see below)
            var storeValue = value;
            storeValue.ClearBit(_timerControlForceLoadBit);
            _timerControl = storeValue;
            if (value.IsBitSet(_timerControlForceLoadBit))
                ResetTimerValue();

            // Handle start bit
            if (value.IsBitSet(_timerControlStartBit))
                StartTimer();
        }
    }

    // Current 16-bit value of the timer, decremented each cycle when timer is running.
    public ushort InternalTimer { get; private set; } = 0;

    private bool _timerIsRunning = false;

    public CiaTimer(CiaTimerType ciaTimerType, IRQSource iRQSource, C64 c64, CiaIRQ ciaIRQ)
    {
        _iRQSource = iRQSource;
        _c64 = c64;
        _ciaIRQ = ciaIRQ;

        _ciaTimerType = ciaTimerType;
        _timerControlRunModeBit = ciaTimerType == CiaTimerType.CiaA ? (int)CiaTimerAControl.TimerARunMode : (int)CiaTimerBControl.TimerBRunMode;
        _timerControlStartBit = ciaTimerType == CiaTimerType.CiaA ? (int)CiaTimerAControl.StartTimerA : (int)CiaTimerBControl.StartTimerB;
        _timerControlForceLoadBit = ciaTimerType == CiaTimerType.CiaA ? (int)CiaTimerAControl.ForceLoadTimerA : (int)CiaTimerBControl.ForceLoadTimerB;
    }

    public void ProcessTimer(ulong cyclesExecuted)
    {
        if (TimerControl.IsBitSet(_timerControlStartBit) && _timerIsRunning)
        {
            if (InternalTimer >= cyclesExecuted)
                InternalTimer -= (ushort)cyclesExecuted;
            else
                InternalTimer = 0;

            if (InternalTimer == 0)
                _ciaIRQ.ConditionSet(_iRQSource);

            if (_ciaIRQ.IsConditionSet(_iRQSource))
            {
                // Timer has reached zero. Trigger interrupt if enabled.
                if (_ciaIRQ.IsEnabled(_iRQSource))
                    _ciaIRQ.Trigger(_iRQSource, _c64.CPU);

                // Check if timer should be reloaded from latch. If Timer A RunMode bit is clear, timer should be continously reloaded from latch.
                if (!TimerControl.IsBitSet(_timerControlRunModeBit))
                {
                    ResetTimerValue();
                }
                else
                {
                    StopTimer();
                }
            }
        }
    }

    private void ResetTimerValue()
    {
        InternalTimer = _internalTimer_Latch;
        StartTimer();
    }

    public void StartTimer()
    {
        _ciaIRQ.ConditionClear(_iRQSource);
        _timerIsRunning = true;
    }

    private void StopTimer()
    {
        var timerControl = TimerControl;
        timerControl.ClearBit(_timerControlStartBit);
        TimerControl = timerControl;

        _timerIsRunning = false;
    }
}

/// <summary>
/// Enum values represents each timer in the CIA chips.
/// </summary>
public enum CiaTimerType
{
    CiaA,
    CiaB,
}

/// <summary>
/// Enum values represents bit position in the CIA Timer A Control register.
/// </summary>
public enum CiaTimerAControl
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
/// Enum values represents bit position in the CIA Timer B Control register.
/// </summary>
public enum CiaTimerBControl
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
