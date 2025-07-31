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
    private readonly Dictionary<CiaTimerType, CiaTimer> _ciaTimers;

    protected CiaBase(C64 c64, CiaIRQ ciaIRQ)
    {
        _c64 = c64;
        _ciaIRQ = ciaIRQ;
        _ciaTimers = new Dictionary<CiaTimerType, CiaTimer>();

        // Initialize timers - this is common for both CIA1 and CIA2
        InitializeTimers();
    }

    /// <summary>
    /// Initialize the timers for this CIA chip
    /// </summary>
    private void InitializeTimers()
    {
        _ciaTimers.Add(CiaTimerType.CiaA, new CiaTimer(CiaTimerType.CiaA, IRQSource.TimerA, _c64, _ciaIRQ));
        _ciaTimers.Add(CiaTimerType.CiaB, new CiaTimer(CiaTimerType.CiaB, IRQSource.TimerB, _c64, _ciaIRQ));
    }

    /// <summary>
    /// Process timers for this CIA chip
    /// </summary>
    /// <param name="cyclesExecuted"></param>
    public virtual void ProcessTimers(ulong cyclesExecuted)
    {
        foreach (var ciaTimer in _ciaTimers.Values)
        {
            ciaTimer.ProcessTimer(cyclesExecuted);
        }
    }

    /// <summary>
    /// Map IO locations for this CIA chip
    /// </summary>
    /// <param name="c64mem"></param>
    public abstract void MapIOLocations(Memory c64mem);

    /// <summary>
    /// Common timer high byte load functionality
    /// </summary>
    protected byte TimerHILoad(CiaTimerType timerType) => _ciaTimers[timerType].InternalTimer.Highbyte();

    /// <summary>
    /// Common timer high byte store functionality
    /// </summary>
    protected void TimerHIStore(CiaTimerType timerType, byte value) => _ciaTimers[timerType].SetInternalTimer_Latch_HI(value);

    /// <summary>
    /// Common timer low byte load functionality
    /// </summary>
    protected byte TimerLOLoad(CiaTimerType timerType) => _ciaTimers[timerType].InternalTimer.Lowbyte();

    /// <summary>
    /// Common timer low byte store functionality
    /// </summary>
    protected void TimerLOStore(CiaTimerType timerType, byte value) => _ciaTimers[timerType].SetInternalTimer_Latch_LO(value);

    /// <summary>
    /// Common timer control load functionality
    /// </summary>
    protected byte TimerControlLoad(CiaTimerType timerType) => _ciaTimers[timerType].TimerControl;

    /// <summary>
    /// Common timer control store functionality
    /// </summary>
    protected void TimerControlStore(CiaTimerType timerType, byte value) => _ciaTimers[timerType].TimerControl = value;

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

        // If any IRQ source is set, also set bit 7.
        if (value != 0)
            value.SetBit((int)IRQSource.Any);

        // If this address is read, it's contents is automatically cleared ( = all IRQ states are cleared).
        _ciaIRQ.ConditionClearAll();

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
