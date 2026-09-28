using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// One of a CIA's two interval timers, with the 6526's pipeline: a control write takes effect
/// through a few cycles of internal state rather than at once. Counting from a start begins so
/// that the first decrement shows three cycles after the write; a force load puts the latch in
/// the counter two cycles after the write and the first decrement from it shows two cycles later;
/// a stop lets the counter move for two more cycles; a one-shot timer stops with the latch in the
/// counter. Writing the latch's high byte while the timer is stopped loads the counter, writing the
/// low byte alone does not. A counting timer never shows 0: the count from 1 is the underflow, and
/// in that cycle the counter already holds the latch, which it keeps for one more cycle before
/// counting on, so the period is the latch + 1 (a latch of 0 counts like 1). A one-shot timer
/// stops in the underflow cycle, and a stop that lands on that cycle leaves the counter at 0.
/// The interrupt flag shows in the interrupt control register from the underflow cycle, and the
/// interrupt output follows a cycle later. On the 6526, an interrupt control read in the cycle
/// before timer B's underflow loses that flag: the read shows it clear and it is never set, while
/// the interrupt output still follows, so a handler then finds only the interrupt bit set.
/// (VICE's cia-timer, reload0 and spritesteal test programs.)
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
    // Whether an interrupt control read in the cycle before the underflow loses the flag (timer B
    // on the 6526).
    private readonly bool _readBeforeUnderflowLosesFlag;

    private readonly CiaTimerType _ciaTimerType;
    private readonly IRQSource _iRQSource;
    private readonly C64 _c64;
    private readonly CiaIRQ _ciaIRQ;
    private readonly CiaBase _cia;
    private readonly int _timerControlStartBit;
    private readonly int _timerControlForceLoadBit;
    private readonly int _timerControlRunModeBit;

    // Latch contains the value was written to timer registers, and is used as start value when timer is started.
    // Reset leaves the timer latches at all ones (the one register the 6526 does not clear), which
    // is what a high-byte write while stopped loads into the counter before the low byte is written.
    private ushort _internalTimer_Latch = 0xFFFF;

    // The contents of the control register for the timer. It's contents is depending on which timer type (A/B) it represents.
    private byte _timerControl = 0;

    // While the timer is counting, its state is the value it shows at a cycle (_counter at
    // _countFrom, one less each cycle after) and the cycle it underflows at (the count from 1, or
    // from 0); the counter value is derived from them and the CIA's current bus cycle on demand
    // (InternalTimer). This keeps the per-instruction catch-up to a comparison. While not counting,
    // and before _countFrom, the counter is _counter.
    private bool _armed;
    private ulong _countFrom;
    private ulong _underflowAtBusCycle;
    private ushort _counter = 0xFFFF;

    // Pending pipeline events, as bus cycles (ulong.MaxValue when none): a force load landing in
    // the counter, and a stop taking effect.
    private ulong _loadAt = ulong.MaxValue;
    private ulong _stopAt = ulong.MaxValue;
    // The cycle a pending underflow flag becomes visible (ulong.MaxValue when none).
    private ulong _flagAt = ulong.MaxValue;
    // The cycle after an underflow, when the interrupt output follows it (ulong.MaxValue when none).
    private ulong _irqAt = ulong.MaxValue;
    // The cycle of the last underflow, which loaded the latch into the counter.
    private ulong _reloadedAt = ulong.MaxValue;
    // Set when a read lost the flag of the coming underflow; its interrupt output still follows.
    private bool _flagLost;

    private ulong Now => _cia.AdvancedToBusCycle;

    private bool StartBitSet => (_timerControl >> _timerControlStartBit & 1) != 0;

    public void SetInternalTimer_Latch_HI(byte highbyte)
    {
        _internalTimer_Latch.SetHighbyte(highbyte);
        ReloadWrittenLatch();
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
        ReloadWrittenLatch();
    }

    // An underflow in the cycle of a latch write reloads the value written in that cycle (the
    // write lands before the reload; reload0's reference data).
    private void ReloadWrittenLatch()
    {
        if (_reloadedAt != Now)
            return;
        _counter = _internalTimer_Latch;
        if (_armed)
        {
            _underflowAtBusCycle = _countFrom + CyclesUntilUnderflow(_internalTimer_Latch);
            ScheduleFlag(_reloadedAt + 2);
        }
        _cia.RecomputeNextUnderflow();
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

            // A force load leaves the interrupt flag alone (reload0's reference data).
            if (forceLoad)
                _loadAt = Now + LoadDelay;             // the load event starts the counting when running
            // A start leaves the interrupt flag alone (ciavarious cia3's reference data).
            if (running && !wasRunning)
            {
                if (valueNow == 0)
                {
                    // A counter of 0 underflows as soon as the start reaches it, before a force load
                    // written with the start lands (reload0's reference data).
                    _counter = 0;
                    _countFrom = Now + StartDelay;
                    _underflowAtBusCycle = Now + StartDelay;
                    _armed = true;
                    ScheduleFlag(Now + StartDelay);
                }
                else if (!forceLoad)
                {
                    ArmFrom(Now + StartDelay, valueNow);
                }
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

    // The counter at a cycle: counting down from _counter at _countFrom. At the underflow cycle
    // itself it is 0; that is only seen when a stop lands on that cycle, since the underflow
    // otherwise loads the latch there.
    private ushort ValueAt(ulong busCycle)
    {
        if (!_armed || busCycle < _countFrom)
            return _counter;
        if (busCycle >= _underflowAtBusCycle)
            return 0;
        return (ushort)(_counter - (busCycle - _countFrom));
    }

    /// <summary>True while the timer is started, i.e. while elapsed cycles change it (after the start's pipeline).</summary>
    public bool IsCounting => StartBitSet;

    /// <summary>The bus cycle of the next thing this timer does (an underflow, a pending load or stop), otherwise <see cref="ulong.MaxValue"/>.</summary>
    internal ulong NextEventBusCycleOrMax
        => Math.Min(Math.Min(Math.Min(_armed ? _underflowAtBusCycle : ulong.MaxValue, _flagAt), Math.Min(_loadAt, _stopAt)), _irqAt);

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
        _readBeforeUnderflowLosesFlag = ciaTimerType == CiaTimerType.CiaB;
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
        _irqAt = ulong.MaxValue;
        _reloadedAt = ulong.MaxValue;
        _flagLost = false;
        Arm();
    }

    /// <summary>
    /// The interrupt control register is being read in the CIA's current cycle. On the 6526, timer
    /// B's flag is lost when that is the cycle before its underflow: the read shows it clear and
    /// the underflow does not set it, but the interrupt output still follows the underflow.
    /// </summary>
    internal void InterruptControlRead()
    {
        if (!_readBeforeUnderflowLosesFlag || !_armed || _underflowAtBusCycle != Now + 1 || _flagAt == ulong.MaxValue)
            return;
        _flagAt = ulong.MaxValue;
        _flagLost = true;
        _cia.RecomputeNextUnderflow();
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
            else if (_irqAt == next)
            {
                // The interrupt output follows the underflow a cycle later, if the source is enabled
                // and its flag has not been read away in the meantime (a flag lost to a read in the
                // cycle before the underflow still drives it); the CPU sees the line from the cycle
                // after that. So an interrupt control read in between still keeps an IRQ (a level,
                // released by the read) from being taken, but not an NMI (an edge, latched when the
                // output goes active). (VICE's irqdelay and timerbasics test programs.)
                _irqAt = ulong.MaxValue;
                if (_ciaIRQ.IsEnabled(_iRQSource) && (_ciaIRQ.IsConditionSet(_iRQSource) || _flagLost))
                    _ciaIRQ.Trigger(_iRQSource, _c64.CPU, next + 1);
                _flagLost = false;
            }
            else if (_stopAt == next)
            {
                // The counter moved through the two cycles after the stop; it stays there now. A stop
                // landing on the underflow cycle wins: no load, the counter stays at 0.
                _counter = ValueAt(next);
                _armed = false;
                _stopAt = ulong.MaxValue;
                _flagAt = ulong.MaxValue;
                _flagLost = false;
            }
            else if (_armed && _underflowAtBusCycle == next)
                Underflow(next);
            else
            {
                // The force load reaches the counter; a running timer counts on from the latch
                // after one held cycle.
                _counter = _internalTimer_Latch;
                _armed = false;
                _loadAt = ulong.MaxValue;
                _flagAt = ulong.MaxValue;
                _flagLost = false;
                if (StartBitSet)
                    ArmFrom(next + 1, _internalTimer_Latch);
            }
        }
    }

    private void Underflow(ulong underflowBusCycle)
    {
        // The count from 1 (or 0): the latch is in the counter from this cycle, and the interrupt
        // output follows a cycle later. The run mode is the one in force before any control write
        // in this cycle.
        _irqAt = underflowBusCycle + 1;
        _reloadedAt = underflowBusCycle;
        if (!_timerControl.IsBitSet(_timerControlRunModeBit))
        {
            // Continuous: the latch shows in this cycle and the next, then counts down.
            _counter = _internalTimer_Latch;
            _countFrom = underflowBusCycle + 1;
            _underflowAtBusCycle = _countFrom + CyclesUntilUnderflow(_internalTimer_Latch);
            ScheduleFlag(underflowBusCycle + 2);
        }
        else
        {
            // One-shot: the timer stops with the latch in the counter and the start bit cleared.
            _armed = false;
            _counter = _internalTimer_Latch;
            _timerControl.ClearBit(_timerControlStartBit);
        }
    }

    // Cycles from the cycle a counter value shows to its underflow: the count from 1 underflows,
    // and a counter of 0 counts like 1.
    private static ulong CyclesUntilUnderflow(ushort counter)
        => counter == 0 ? 1UL : counter;

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

    // The flag shows in the interrupt control register from the underflow cycle (the interrupt
    // output follows a cycle after it); not before the cycle the counting starts from. Dated from
    // that cycle, not from where the CIA has caught up to, since the catch-up may already be past it.
    private void ScheduleFlag(ulong fromBusCycle)
        => _flagAt = Math.Max(_underflowAtBusCycle, fromBusCycle);

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
        _irqAt = ulong.MaxValue;
        _reloadedAt = ulong.MaxValue;
        _flagLost = false;
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
