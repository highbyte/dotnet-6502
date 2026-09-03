using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public class CiaTimer
{
    private readonly CiaTimerType _ciaTimerType;
    private readonly IRQSource _iRQSource;
    private readonly C64 _c64;
    private readonly CiaIRQ _ciaIRQ;
    private readonly CiaBase _cia;
    private readonly int _timerControlStartBit;
    private readonly int _timerControlForceLoadBit;
    private readonly int _timerControlRunModeBit;

    // Latch contains the value was written to timer registers, and is used as start value when timer is started.
    private ushort _internalTimer_Latch = 0;

    // The contents of the control register for the timer. It's contents is depending on which timer type (A/B) it represents.
    private byte _timerControl = 0;

    private bool _timerIsRunning = false;

    // While the timer is counting, its state is the bus cycle at which it underflows; the counter
    // value is derived from that and the CIA's current bus cycle on demand (InternalTimer). This
    // keeps the per-instruction catch-up to a comparison. While not counting, the counter is held
    // in _counter.
    private bool _armed;
    private ulong _underflowAtBusCycle;
    private ushort _counter;

    private ulong Now => _cia.AdvancedToBusCycle;

    public void SetInternalTimer_Latch_HI(byte highbyte)
    {
        _internalTimer_Latch.SetHighbyte(highbyte);
        // If timer is not running, set the internal timer to the latch value.
        if (!_timerIsRunning)
            _counter = _internalTimer_Latch;
    }
    public void SetInternalTimer_Latch_LO(byte lowbyte)
    {
        _internalTimer_Latch.SetLowbyte(lowbyte);
        // If timer is not running, set the internal timer to the latch value.
        if (!_timerIsRunning)
            _counter = _internalTimer_Latch;
    }

    public byte TimerControl
    {
        get
        {
            return _timerControl;
        }
        set
        {
            Freeze();

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

            Arm();
        }
    }

    /// <summary>Current 16-bit value of the timer, decremented each cycle when timer is running.</summary>
    public ushort InternalTimer => _armed ? (ushort)(_underflowAtBusCycle - Now - 1) : _counter;

    /// <summary>True while the timer is started and running, i.e. while elapsed cycles change it.</summary>
    public bool IsCounting => _timerIsRunning && (_timerControl >> _timerControlStartBit & 1) != 0;

    /// <summary>The bus cycle of the next underflow while counting, otherwise <see cref="ulong.MaxValue"/>.</summary>
    internal ulong UnderflowBusCycleOrMax => _armed ? _underflowAtBusCycle : ulong.MaxValue;

    public CiaTimer(CiaTimerType ciaTimerType, IRQSource iRQSource, C64 c64, CiaIRQ ciaIRQ, CiaBase cia)
    {
        _iRQSource = iRQSource;
        _c64 = c64;
        _ciaIRQ = ciaIRQ;
        _cia = cia;

        _ciaTimerType = ciaTimerType;
        _timerControlRunModeBit = ciaTimerType == CiaTimerType.CiaA ? (int)CiaTimerAControl.TimerARunMode : (int)CiaTimerBControl.TimerBRunMode;
        _timerControlStartBit = ciaTimerType == CiaTimerType.CiaA ? (int)CiaTimerAControl.StartTimerA : (int)CiaTimerBControl.StartTimerB;
        _timerControlForceLoadBit = ciaTimerType == CiaTimerType.CiaA ? (int)CiaTimerAControl.ForceLoadTimerA : (int)CiaTimerBControl.ForceLoadTimerB;
    }

    // --- Snapshot support ---
    // Captures/restores the live timer state that is not held in the C64 IO register storage:
    // the latch, the control byte, the current counter and the running flag. Restore sets the
    // fields directly (bypassing the TimerControl setter) so the exact preserved state is applied
    // without re-triggering force-load/start side effects.
    internal (ushort Latch, byte Control, ushort Current, bool Running) GetSnapshotState()
        => (_internalTimer_Latch, _timerControl, InternalTimer, _timerIsRunning);

    internal void RestoreSnapshotState(ushort latch, byte control, ushort current, bool running)
    {
        _internalTimer_Latch = latch;
        _timerControl = control;
        _counter = current;
        _timerIsRunning = running;
        _armed = false;
        Arm();
    }

    /// <summary>
    /// Advance the timer by a number of cycles from where the CIA is. Tests and tooling; the C64
    /// drives the CIA through <see cref="CiaBase.CatchUpTo"/>.
    /// </summary>
    public void ProcessTimer(ulong cyclesExecuted) => _cia.CatchUpTo(Now + cyclesExecuted);

    /// <summary>
    /// Handle every underflow up to and including the given bus cycle (the CIA has already moved
    /// its position there). Each underflow is dated to its own cycle so the interrupt carries its
    /// real cycle.
    /// </summary>
    internal void ProcessUnderflows(ulong busCycle)
    {
        while (_armed && busCycle >= _underflowAtBusCycle)
        {
            var underflowBusCycle = _underflowAtBusCycle;
            _ciaIRQ.ConditionSet(_iRQSource);

            // Timer has reached zero. Trigger interrupt if enabled.
            if (_ciaIRQ.IsEnabled(_iRQSource))
                _ciaIRQ.Trigger(_iRQSource, _c64.CPU, underflowBusCycle);

            // Check if timer should be reloaded from latch. If the RunMode bit is clear, timer should be continuously reloaded from latch.
            if (!_timerControl.IsBitSet(_timerControlRunModeBit))
            {
                _timerIsRunning = true;
                _underflowAtBusCycle = underflowBusCycle + CyclesUntilUnderflow(_internalTimer_Latch);
            }
            else
            {
                StopTimer();
            }
        }
    }

    // A counter of 0 wraps through 0x10000 cycles before it underflows.
    private static ulong CyclesUntilUnderflow(ushort counter)
        => counter == 0
            ? 0x10000UL
            : (ulong)counter + 1;

    /// <summary>Switch a counting timer to the deadline representation from its stored counter.</summary>
    internal void Arm()
    {
        if (!_armed && IsCounting)
        {
            _underflowAtBusCycle = Now + CyclesUntilUnderflow(_counter);
            _armed = true;
        }
        _cia.RecomputeNextUnderflow();
    }

    /// <summary>Store the derived counter and leave the deadline representation.</summary>
    internal void Freeze()
    {
        if (!_armed)
            return;
        _counter = InternalTimer;
        _armed = false;
        _cia.RecomputeNextUnderflow();
    }

    private void ResetTimerValue()
    {
        _counter = _internalTimer_Latch;
        StartTimer();
    }

    public void StartTimer()
    {
        _ciaIRQ.ConditionClear(_iRQSource);
        _timerIsRunning = true;
    }

    private void StopTimer()
    {
        // Called at a one-shot underflow: the timer stops with the wrapped counter.
        _armed = false;
        _counter = 0xffff;
        _timerControl.ClearBit(_timerControlStartBit);
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
