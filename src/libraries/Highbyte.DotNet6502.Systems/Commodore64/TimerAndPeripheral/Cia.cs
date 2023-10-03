namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// Complex Interface Adapter (CIA) chip.
/// Used for timers and communication with peripheral input and output (keyboard, joystick).
/// </summary>
public class Cia
{
    private readonly C64 _c64;

    public CiaIRQ CiaIRQ { get; private set; }

    public Dictionary<CiaTimerType, CiaTimer> CiaTimers { get; private set; }

    public Cia(C64 c64)
    {
        _c64 = c64;
        CiaIRQ = new CiaIRQ();

        CiaTimers = new();
        CiaTimers.Add(CiaTimerType.Cia1_A, new CiaTimer(CiaTimerType.Cia1_A, IRQSource.TimerA, _c64));
        CiaTimers.Add(CiaTimerType.Cia1_B, new CiaTimer(CiaTimerType.Cia1_B, IRQSource.TimerB, _c64));
    }

    public void ProcessTimers(ulong cyclesExecuted)
    {
        foreach (var ciaTimer in CiaTimers.Values)
        {
            ciaTimer.ProcessTimer(cyclesExecuted);
        }
    }

    public void MapIOLocations(Memory mem)
    {
        // CIA #1 DataPort A
        mem.MapReader(CiaAddr.CIA1_DATAA, Cia1DataALoad);
        mem.MapWriter(CiaAddr.CIA1_DATAA, Cia1DataAStore);

        // CIA #1 DataPort B
        // Workaround set 0xff in data port B as initial value. Means no key down (temporary solution, which is useful to not get extremely long execution of Kernal routines inspecting keyboard.
        _c64.WriteIOStorage(CiaAddr.CIA1_DATAB, 0xff);
        mem.MapReader(CiaAddr.CIA1_DATAB, Cia1DataBLoad);
        mem.MapWriter(CiaAddr.CIA1_DATAB, Cia1DataBStore);

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

    // TODO: Implement "real" C64 keyboard operation emulation.
    //       Right now, keys are being placed directly into the ring buffer, and not via Cia1 Data Ports A & B
    public byte Cia1DataALoad(ushort _) => 0;
    public void Cia1DataAStore(ushort _, byte value) { }

    // TODO: Implement "real" C64 keyboard operation emulation.
    //       Right now, keys are being placed directly into the ring buffer, and not via Cia1 Data Ports A & B
    public byte Cia1DataBLoad(ushort address) => _c64.ReadIOStorage(address);
    public void Cia1DataBStore(ushort address, byte value) { _c64.WriteIOStorage(address, value); }

    public byte Cia1TimerAHILoad(ushort _) => CiaTimers[CiaTimerType.Cia1_A].InternalTimer.Highbyte();
    public void Cia1TimerAHIStore(ushort _, byte value) => CiaTimers[CiaTimerType.Cia1_A].SetInternalTimer_Latch_HI(value);

    public byte Cia1TimerALOLoad(ushort _) => CiaTimers[CiaTimerType.Cia1_A].InternalTimer.Lowbyte();
    public void Cia1TimerALOStore(ushort _, byte value) => CiaTimers[CiaTimerType.Cia1_A].SetInternalTimer_Latch_LO(value);

    public byte Cia1TimerBHILoad(ushort _) => CiaTimers[CiaTimerType.Cia1_B].InternalTimer.Highbyte();
    public void Cia1TimerBHIStore(ushort _, byte value) => CiaTimers[CiaTimerType.Cia1_B].SetInternalTimer_Latch_HI(value);

    public byte Cia1TimerBLOLoad(ushort _) => CiaTimers[CiaTimerType.Cia1_B].InternalTimer.Lowbyte();
    public void Cia1TimerBLOStore(ushort _, byte value) => CiaTimers[CiaTimerType.Cia1_B].SetInternalTimer_Latch_LO(value);

    public byte Cia1InteruptControlLoad(ushort _)
    {
        // Bits 5-6 are not used, and always returns 0.
        byte value = 0;

        // If timer A has counted down to zero, set bit 0.
        if (CiaIRQ.IsConditionSet(IRQSource.TimerA))
            value.SetBit((int)IRQSource.TimerA);

        // If timer B has counted down to zero, set bit 1.
        if (CiaIRQ.IsConditionSet(IRQSource.TimerB))
            value.SetBit((int)IRQSource.TimerB);

        // If any IRQ source is set, also set bit 7.
        if (value != 0)
            value.SetBit((int)IRQSource.Any);

        // If this address is read, it's contents is automatically cleared ( = all IRQ states are cleared).
        CiaIRQ.ConditionClearAll();

        return value;
    }
    public void Cia1InteruptControlStore(ushort _, byte value)
    {
        // Writing to this register enables or disables the different interrupt sources.
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
                    CiaIRQ.Enable(source);
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
                    CiaIRQ.Disable(source);
            }
        }
    }

    public byte Cia1TimerAControlLoad(ushort _) => CiaTimers[CiaTimerType.Cia1_A].TimerControl;
    public void Cia1TimerAControlStore(ushort _, byte value) => CiaTimers[CiaTimerType.Cia1_A].TimerControl = value;

    public byte Cia1TimerBControlLoad(ushort _) => CiaTimers[CiaTimerType.Cia1_B].TimerControl;
    public void Cia1TimerBControlStore(ushort _, byte value) => CiaTimers[CiaTimerType.Cia1_B].TimerControl = value;
}
