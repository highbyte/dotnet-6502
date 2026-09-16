using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// One of a CIA's two interval timers, with the 6526's pipeline: a control write takes effect
/// through a few cycles of internal state rather than at once. Counting from a start begins so
/// that the first decrement shows three cycles after the write; a force load puts the latch in
/// the counter two cycles after the write and the first decrement from it shows two cycles later;
/// a stop lets the counter move for two more cycles; a one-shot timer stops with the latch in the
/// counter. Writing the latch's high byte while the timer is stopped loads the counter, writing the
/// low byte alone does not. (VICE's cia-timer, reload0 and spritesteal test programs.)
/// </summary>
public class CiaTimer
{
    // Cycles from a control write to the first decrement, less one: the counter holds its value
    // through write + 2 and shows the first decrement at write + 3.
    private const int StartDelay = 2;
    // Cycles from a force-load write to the latch showing in the counter.
    private const int LoadDelay = 2;
    // Cycles the counter still moves after a stop is written.
    private const int StopLag = 2;
    // Cycles before the underflow cycle at which the interrupt flag shows in the interrupt control
    // register: one for timer A, two for timer B on the 6526 (VICE's cia-timer test, old CIAs).
    private readonly ulong _flagLead;

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

    // While the timer is counting, its state is the bus cycle at which it underflows; the counter
    // value is derived from that and the CIA's current bus cycle on demand (InternalTimer). This
    // keeps the per-instruction catch-up to a comparison. While not counting, and before the cycle
    // the counting starts at (_countFrom), the counter is held in _counter.
    private bool _armed;
    private ulong _countFrom;
    private ulong _underflowAtBusCycle;
    private ushort _counter;

    // Pending pipeline events, as bus cycles (ulong.MaxValue when none): a force load landing in
    // the counter, and a stop taking effect.
    private ulong _loadAt = ulong.MaxValue;
    private ulong _stopAt = ulong.MaxValue;
    // The cycle a pending underflow flag becomes visible (ulong.MaxValue when none).
    private ulong _flagAt = ulong.MaxValue;

    private ulong Now => _cia.AdvancedToBusCycle;

    private bool StartBitSet => (_timerControl >> _timerControlStartBit & 1) != 0;

    public void SetInternalTimer_Latch_HI(byte highbyte)
    {
        _internalTimer_Latch.SetHighbyte(highbyte);
        // While the timer is stopped, the write loads the whole latch into the counter.
        if (!StartBitSet)
        {
            _armed = false;
            _stopAt = ulong.MaxValue;
            _counter = _internalTimer_Latch;
            _cia.RecomputeNextUnderflow();
        }
    }

    public void SetInternalTimer_Latch_LO(byte lowbyte)
    {
        // The low byte only reaches the counter through a load (force load, underflow, or the high
        // byte's write while stopped).
        _internalTimer_Latch.SetLowbyte(lowbyte);
    }

    public byte TimerControl
    {
        get
        {
            return _timerControl;
        }
        set
        {
            var wasRunning = StartBitSet;
            var valueNow = InternalTimer;

            // Bit 4 (force latch load) is a command, not stored.
            var storeValue = value;
            storeValue.ClearBit(_timerControlForceLoadBit);
            _timerControl = storeValue;
            var forceLoad = value.IsBitSet(_timerControlForceLoadBit);
            var running = StartBitSet;

            // A new write supersedes what the last one left pending.
            _loadAt = ulong.MaxValue;
            _stopAt = ulong.MaxValue;

            if (forceLoad)
            {
                _ciaIRQ.ConditionClear(_iRQSource);
                _loadAt = Now + LoadDelay;             // the load event starts the counting when running
            }
            if (running && !wasRunning)
            {
                _ciaIRQ.ConditionClear(_iRQSource);
                if (!forceLoad)
                    ArmFrom(Now + StartDelay, valueNow);
            }
            else if (!running && wasRunning && _armed)
            {
                _stopAt = Now + StopLag;
            }

            _cia.RecomputeNextUnderflow();
        }
    }

    /// <summary>Current 16-bit value of the timer, decremented each cycle while it is counting.</summary>
    public ushort InternalTimer => ValueAt(Now);

    private ushort ValueAt(ulong busCycle)
        => _armed && busCycle >= _countFrom ? (ushort)(_underflowAtBusCycle - busCycle - 1) : _counter;

    /// <summary>True while the timer is started, i.e. while elapsed cycles change it (after the start's pipeline).</summary>
    public bool IsCounting => StartBitSet;

    /// <summary>The bus cycle of the next thing this timer does (an underflow, a pending load or stop), otherwise <see cref="ulong.MaxValue"/>.</summary>
    internal ulong NextEventBusCycleOrMax
        => Math.Min(Math.Min(_armed ? _underflowAtBusCycle : ulong.MaxValue, _flagAt), Math.Min(_loadAt, _stopAt));

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
        _flagLead = ciaTimerType == CiaTimerType.CiaA ? 1UL : 2UL;
    }

    // --- Snapshot support ---
    // Captures/restores the live timer state that is not held in the C64 IO register storage:
    // the latch, the control byte, the current counter and the running flag. Restore sets the
    // fields directly (bypassing the TimerControl setter) so the exact preserved state is applied
    // without re-triggering force-load/start side effects. A load or stop still in the pipeline
    // at the snapshot is not carried over.
    internal (ushort Latch, byte Control, ushort Current, bool Running) GetSnapshotState()
        => (_internalTimer_Latch, _timerControl, InternalTimer, StartBitSet);

    internal void RestoreSnapshotState(ushort latch, byte control, ushort current, bool running)
    {
        _internalTimer_Latch = latch;
        _timerControl = control;
        if (running)
            _timerControl.SetBit(_timerControlStartBit);
        else
            _timerControl.ClearBit(_timerControlStartBit);
        _counter = current;
        _armed = false;
        _loadAt = ulong.MaxValue;
        _stopAt = ulong.MaxValue;
        _flagAt = ulong.MaxValue;
        Arm();
    }

    /// <summary>
    /// Advance the timer by a number of cycles from where the CIA is. Tests and tooling; the C64
    /// drives the CIA through <see cref="CiaBase.CatchUpTo"/>.
    /// </summary>
    public void ProcessTimer(ulong cyclesExecuted) => _cia.CatchUpTo(Now + cyclesExecuted);

    /// <summary>
    /// Handle every event up to and including the given bus cycle (the CIA has already moved its
    /// position there): underflows, and the loads and stops the pipeline has pending. Each is
    /// dated to its own cycle so an interrupt carries its real cycle.
    /// </summary>
    internal void ProcessEvents(ulong busCycle)
    {
        while (true)
        {
            var next = NextEventBusCycleOrMax;
            if (next > busCycle)
                break;
            if (_flagAt == next)
            {
                _flagAt = ulong.MaxValue;
                _ciaIRQ.ConditionSet(_iRQSource);
            }
            else if (_armed && _underflowAtBusCycle == next)
                Underflow(next);
            else if (_stopAt == next)
            {
                // The counter moved through the two cycles after the stop; it stays there now.
                _counter = ValueAt(next);
                _armed = false;
                _stopAt = ulong.MaxValue;
                _flagAt = ulong.MaxValue;
            }
            else
            {
                // The force load reaches the counter; a running timer counts on from the latch
                // after one held cycle.
                _counter = _internalTimer_Latch;
                _armed = false;
                _loadAt = ulong.MaxValue;
                _flagAt = ulong.MaxValue;
                if (StartBitSet)
                    ArmFrom(next + 1, _internalTimer_Latch);
            }
        }
    }

    private void Underflow(ulong underflowBusCycle)
    {
        // The interrupt output is asserted for the underflow cycle if the source is enabled and its
        // flag, shown a cycle earlier, has not been read away in the meantime (the flag itself was
        // scheduled ahead of this cycle when the counting was armed).
        if (_ciaIRQ.IsEnabled(_iRQSource) && _ciaIRQ.IsConditionSet(_iRQSource))
            _ciaIRQ.Trigger(_iRQSource, _c64.CPU, underflowBusCycle);

        if (!_timerControl.IsBitSet(_timerControlRunModeBit))
        {
            // Continuous: the latch is in the counter at the underflow cycle and counts on.
            _countFrom = underflowBusCycle;
            _underflowAtBusCycle = underflowBusCycle + CyclesUntilUnderflow(_internalTimer_Latch);
            ScheduleFlag(underflowBusCycle + 1);
        }
        else
        {
            // One-shot: the timer stops with the latch in the counter and the start bit cleared.
            _armed = false;
            _counter = _internalTimer_Latch;
            _timerControl.ClearBit(_timerControlStartBit);
        }
    }

    // A counter of 0 wraps through 0x10000 cycles before it underflows.
    private static ulong CyclesUntilUnderflow(ushort counter)
        => counter == 0
            ? 0x10000UL
            : (ulong)counter + 1;

    // Count from the given cycle on, starting at the given value: the counter shows that value at
    // that cycle and one less at the next.
    private void ArmFrom(ulong fromBusCycle, ushort counter)
    {
        _counter = counter;
        _countFrom = fromBusCycle;
        _underflowAtBusCycle = fromBusCycle + CyclesUntilUnderflow(counter);
        _armed = true;
        ScheduleFlag(fromBusCycle);
    }

    // The flag shows in the interrupt control register a cycle or two before the underflow cycle
    // (the cycle the counter reads 0, or 1), as the 6526's reads of it show; not before the cycle
    // the counting starts from. Dated from that cycle, not from where the CIA has caught up to,
    // since the catch-up may already be past it.
    private void ScheduleFlag(ulong fromBusCycle)
        => _flagAt = Math.Max(_underflowAtBusCycle - _flagLead, fromBusCycle);

    /// <summary>Switch a started timer to the deadline representation from its stored counter, counting from now.</summary>
    internal void Arm()
    {
        if (!_armed && StartBitSet)
            ArmFrom(Now, _counter);
        _cia.RecomputeNextUnderflow();
    }

    /// <summary>Store the derived counter and leave the deadline representation; pending pipeline events are dropped.</summary>
    internal void Freeze()
    {
        _loadAt = ulong.MaxValue;
        _stopAt = ulong.MaxValue;
        if (!_armed)
            return;
        _counter = InternalTimer;
        _armed = false;
        _cia.RecomputeNextUnderflow();
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
