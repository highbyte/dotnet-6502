using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// Complex Interface Adapter (CIA) chip.
/// Used for timers and communication with peripheral input and output (keyboard, joystick).
/// </summary>
public class Cia
{
    private readonly C64 _c64;

    public CiaIRQ CiaIRQ { get; private set; }
    public C64Keyboard Keyboard { get; private set; }
    public C64Joystick Joystick { get; private set; }
    public Dictionary<CiaTimerType, CiaTimer> CiaTimers { get; private set; }

    public Cia(C64 c64, Config.C64Config c64Config, ILoggerFactory loggerFactory)
    {
        _c64 = c64;
        CiaIRQ = new CiaIRQ();
        Keyboard = new C64Keyboard(c64, loggerFactory);
        Joystick = new C64Joystick(c64Config, loggerFactory);

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

    public void MapIOLocations(Memory c64mem)
    {
        // CIA #1 DataPort A
        c64mem.MapReader(CiaAddr.CIA1_DATAA, Cia1DataALoad);
        c64mem.MapWriter(CiaAddr.CIA1_DATAA, Cia1DataAStore);

        // CIA #1 DataPort B
        c64mem.MapReader(CiaAddr.CIA1_DATAB, Cia1DataBLoad);
        c64mem.MapWriter(CiaAddr.CIA1_DATAB, Cia1DataBStore);

        // CIA #1 Timer A
        c64mem.MapReader(CiaAddr.CIA1_TIMAHI, Cia1TimerAHILoad);
        c64mem.MapWriter(CiaAddr.CIA1_TIMAHI, Cia1TimerAHIStore);

        c64mem.MapReader(CiaAddr.CIA1_TIMALO, Cia1TimerALOLoad);
        c64mem.MapWriter(CiaAddr.CIA1_TIMALO, Cia1TimerALOStore);

        // CIA #1 Timer B
        c64mem.MapReader(CiaAddr.CIA1_TIMBHI, Cia1TimerBHILoad);
        c64mem.MapWriter(CiaAddr.CIA1_TIMBHI, Cia1TimerBHIStore);

        c64mem.MapReader(CiaAddr.CIA1_TIMBLO, Cia1TimerBLOLoad);
        c64mem.MapWriter(CiaAddr.CIA1_TIMBLO, Cia1TimerBLOStore);

        // CIA #1 Interrupt Control Register
        c64mem.MapReader(CiaAddr.CIA1_CIAICR, Cia1InteruptControlLoad);
        c64mem.MapWriter(CiaAddr.CIA1_CIAICR, Cia1InteruptControlStore);

        // CIA #1 Control Register A
        c64mem.MapReader(CiaAddr.CIA1_CIACRA, Cia1TimerAControlLoad);
        c64mem.MapWriter(CiaAddr.CIA1_CIACRA, Cia1TimerAControlStore);

        // CIA #1 Control Register B
        c64mem.MapReader(CiaAddr.CIA1_CIACRB, Cia1TimerBControlLoad);
        c64mem.MapWriter(CiaAddr.CIA1_CIACRB, Cia1TimerBControlStore);

    }

    // Cia 1 Data Port A is normally read from to get joystick (#2) input.
    // It's written to to control which keys can be read from Cia 1 Data Port B.
    public byte Cia1DataALoad(ushort _)
    {
        //// Temporary workaround, set the default state
        //byte value = 0x7f;

        var value = Keyboard.GetSelectedMatrixRow();

        // Also set Joystick #2 bits
        foreach (var action in Joystick.CurrentJoystick2Actions)
        {
            value.ClearBit((int)action);
        }

        return value;
    }

    // Writing to Cia 1 Data Port A controls which keys can be read from Cia 1 Data Port B.
    public void Cia1DataAStore(ushort address, byte value)
    {
        Keyboard.SetSelectedMatrixRow(value);
    }

    // When reading from Cia 1 Data Port B you can get both keyboard and joystick (#1) input sharing the same bits (which can be confusing).
    public byte Cia1DataBLoad(ushort address)
    {
        // Get the pressed keys for the selected matrix row (set by writing to Cia 1 Data Port A DC00)
        var value = Keyboard.GetPressedKeysForSelectedMatrixRow();

        // Also set Joystick #1 bits
        foreach (var action in Joystick.CurrentJoystick1Actions)
        {
            value.ClearBit((int)action);
        }
        return value;
    }
    public void Cia1DataBStore(ushort address, byte value)
    {
        // TODO: What will writing to this address affect?
        _c64.WriteIOStorage(address, value);
    }

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
