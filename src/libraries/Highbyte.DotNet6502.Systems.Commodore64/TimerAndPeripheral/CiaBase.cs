using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// Base class for Complex Interface Adapter (CIA) chip functionality.
/// Contains common functionality shared between CIA 1 and CIA 2.
/// </summary>
public abstract class CiaBase
{
    protected readonly C64 _c64;

    private readonly CiaIRQ _ciaIRQ;
    private readonly CiaTimer _timerA;
    private readonly CiaTimer _timerB;

    /// <summary>
    /// The CPU bus cycle (<see cref="CPU.BusCycles"/>) the timers have been advanced to. See
    /// <see cref="CatchUpTo"/>.
    /// </summary>
    private ulong _advancedToBusCycle;

    protected CiaBase(C64 c64, CiaIRQ ciaIRQ)
    {
        _c64 = c64;
        _ciaIRQ = ciaIRQ;
        _timerA = new CiaTimer(CiaTimerType.CiaA, IRQSource.TimerA, _c64, _ciaIRQ);
        _timerB = new CiaTimer(CiaTimerType.CiaB, IRQSource.TimerB, _c64, _ciaIRQ);
    }

    private CiaTimer Timer(CiaTimerType timerType) => timerType == CiaTimerType.CiaA ? _timerA : _timerB;

    // --- Snapshot support ---
    // Exposes the live timer and IRQ state (not held in IO register storage) to the c64-cia
    // snapshot module, which lives in the same assembly.
    internal CiaTimer SnapshotTimerA => _timerA;
    internal CiaTimer SnapshotTimerB => _timerB;
    internal CiaIRQ SnapshotIrq => _ciaIRQ;

    /// <summary>
    /// Advance the timers by a number of cycles, independent of the CPU bus-cycle counter. The C64
    /// drives the CIAs through <see cref="CatchUpTo"/>; this remains for tests and tooling.
    /// </summary>
    public virtual void ProcessTimers(ulong cyclesExecuted) => ProcessTimers(cyclesExecuted, _c64.CPU.BusCycles);

    private void ProcessTimers(ulong cyclesExecuted, ulong endBusCycle)
    {
        _timerA.ProcessTimer(cyclesExecuted, endBusCycle);
        _timerB.ProcessTimer(cyclesExecuted, endBusCycle);
    }

    /// <summary>
    /// Advance the timers to the given CPU bus cycle. No-op if they are already there. Depending
    /// on <see cref="C64.TimerMode"/> the C64 calls this at every instruction boundary or at every
    /// raster line change; every CIA register access calls it for the cycle of the access, so a
    /// timer or interrupt-status read sees the count at its own cycle and a control write takes
    /// effect on its own cycle.
    /// </summary>
    public void CatchUpTo(ulong busCycle)
    {
        if (busCycle <= _advancedToBusCycle)
            return;
        var cycles = busCycle - _advancedToBusCycle;
        _advancedToBusCycle = busCycle;
        // Idle timers do not change with time; skip the per-timer calls on the common path.
        if (!_timerA.IsCounting && !_timerB.IsCounting)
            return;
        ProcessTimers(cycles, busCycle);
    }

    /// <summary>State as of the cycle of the bus access the CPU is performing right now.</summary>
    private void CatchUpToCurrentAccess()
    {
        var busCycles = _c64.CPU.BusCycles;
        if (busCycles > 0)
            CatchUpTo(busCycles - 1);
    }

    /// <summary>
    /// Realign the bus-cycle bookkeeping with the CPU without advancing the timers. Used after a
    /// snapshot restore.
    /// </summary>
    internal void ResyncToBusCycle() => _advancedToBusCycle = _c64.CPU.BusCycles;

    /// <summary>
    /// Map IO locations for this CIA chip
    /// </summary>
    /// <param name="c64mem"></param>
    public abstract void MapIOLocations(Memory c64mem);

    /// <summary>
    /// Map one CIA register and all of its mirrors across the chip's 256-byte I/O page.
    /// The MOS 6526 only decodes the low 4 address bits, so $DC0D is also visible at
    /// $DC1D, $DC2D, ..., $DCFD (and likewise for CIA #2 at $DDxx). Every access first advances
    /// the timers to the cycle of the access.
    /// </summary>
    protected void MapRegisterMirrors(
        Memory c64mem,
        ushort registerAddress,
        Memory.LoadByte reader,
        Memory.StoreByte writer)
    {
        Memory.LoadByte timedReader = _ =>
        {
            CatchUpToCurrentAccess();
            return reader(registerAddress);
        };
        Memory.StoreByte timedWriter = (_, value) =>
        {
            CatchUpToCurrentAccess();
            writer(registerAddress, value);
        };

        var pageStart = registerAddress & 0xFF00;
        var registerOffset = registerAddress & 0x000F;

        for (var offset = registerOffset; offset <= 0x00FF; offset += 0x10)
        {
            var mirrorAddress = (ushort)(pageStart + offset);
            c64mem.MapReader(mirrorAddress, timedReader);
            c64mem.MapWriter(mirrorAddress, timedWriter);
        }
    }

    /// <summary>
    /// Common timer high byte load functionality
    /// </summary>
    protected byte TimerHILoad(CiaTimerType timerType) => Timer(timerType).InternalTimer.Highbyte();

    /// <summary>
    /// Common timer high byte store functionality
    /// </summary>
    protected void TimerHIStore(CiaTimerType timerType, byte value) => Timer(timerType).SetInternalTimer_Latch_HI(value);

    /// <summary>
    /// Common timer low byte load functionality
    /// </summary>
    protected byte TimerLOLoad(CiaTimerType timerType) => Timer(timerType).InternalTimer.Lowbyte();

    /// <summary>
    /// Common timer low byte store functionality
    /// </summary>
    protected void TimerLOStore(CiaTimerType timerType, byte value) => Timer(timerType).SetInternalTimer_Latch_LO(value);

    /// <summary>
    /// Common timer control load functionality
    /// </summary>
    protected byte TimerControlLoad(CiaTimerType timerType) => Timer(timerType).TimerControl;

    /// <summary>
    /// Common timer control store functionality
    /// </summary>
    protected void TimerControlStore(CiaTimerType timerType, byte value) => Timer(timerType).TimerControl = value;

    /// <summary>
    /// Common timer A methods
    /// </summary>
    public byte TimerAHILoad(ushort _) => TimerHILoad(CiaTimerType.CiaA);
    public void TimerAHIStore(ushort _, byte value) => TimerHIStore(CiaTimerType.CiaA, value);
    public byte TimerALOLoad(ushort _) => TimerLOLoad(CiaTimerType.CiaA);
    public void TimerALOStore(ushort _, byte value) => TimerLOStore(CiaTimerType.CiaA, value);
    public byte TimerAControlLoad(ushort _) => TimerControlLoad(CiaTimerType.CiaA);
    public void TimerAControlStore(ushort _, byte value) => TimerControlStore(CiaTimerType.CiaA, value);

    /// <summary>
    /// Common timer B methods
    /// </summary>
    public byte TimerBHILoad(ushort _) => TimerHILoad(CiaTimerType.CiaB);
    public void TimerBHIStore(ushort _, byte value) => TimerHIStore(CiaTimerType.CiaB, value);
    public byte TimerBLOLoad(ushort _) => TimerLOLoad(CiaTimerType.CiaB);
    public void TimerBLOStore(ushort _, byte value) => TimerLOStore(CiaTimerType.CiaB, value);
    public byte TimerBControlLoad(ushort _) => TimerControlLoad(CiaTimerType.CiaB);
    public void TimerBControlStore(ushort _, byte value) => TimerControlStore(CiaTimerType.CiaB, value);

    /// <summary>
    /// Common interrupt control load functionality
    /// </summary>
    protected byte InterruptControlLoad()
    {
        // Bits 5-6 are not used, and always returns 0.
        byte value = 0;

        // If timer A has counted down to zero, set bit 0.
        if (_ciaIRQ.IsConditionSet(IRQSource.TimerA))
            value.SetBit((int)IRQSource.TimerA);

        // If timer B has counted down to zero, set bit 1.
        if (_ciaIRQ.IsConditionSet(IRQSource.TimerB))
            value.SetBit((int)IRQSource.TimerB);

        // Bit 7 is the interrupt-request latch. A CIA source condition can be set
        // while its mask is disabled; in that case the source bit is reported, but
        // bit 7 must stay clear because the CIA did not actually drive IRQ/NMI.
        if (_ciaIRQ.IsConditionSet(IRQSource.Any))
            value.SetBit((int)IRQSource.Any);

        // If this address is read, it's contents is automatically cleared ( = all IRQ states are cleared).
        _ciaIRQ.ConditionClearAll();
        _ciaIRQ.Acknowledge(_c64.CPU);

        return value;
    }

    /// <summary>
    /// Common interrupt control store functionality
    /// </summary>
    protected void InterruptControlStore(byte value)
    {
        // Writing to this register enables or disables the different interrupt sources.
        // If bit 7 is set, then other bit also set means to enable that interrupt source.
        // If bit 7 is not set, then other bit also set means to disable that interrupt source.
        // If bits for the specific interrupt sources (0-4) are not set, it will not change state.

        if ((value & 0b1000_0000) > 0)
        {
            // Bit 7 is set, enable interrupt sources with bit set
            foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
            {
                if (source == IRQSource.Any)
                    continue;
                if (value.IsBitSet((int)source))
                    _ciaIRQ.Enable(source);
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
                    _ciaIRQ.Disable(source);
            }
        }
    }

    /// <summary>
    /// Common interrupt control methods
    /// </summary>
    public byte InterruptControlLoad(ushort _) => InterruptControlLoad();
    public void InterruptControlStore(ushort _, byte value) => InterruptControlStore(value);

    /// <summary>
    /// Debug load functionality - reads from IO storage
    /// </summary>
    public byte DebugLoad(ushort address) => _c64.ReadIOStorage(address);

    /// <summary>
    /// Debug store functionality - writes to IO storage
    /// </summary>
    public void DebugStore(ushort address, byte value) => _c64.WriteIOStorage(address, value);
}
