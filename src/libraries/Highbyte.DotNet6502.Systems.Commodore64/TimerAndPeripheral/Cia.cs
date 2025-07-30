using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;
using Highbyte.DotNet6502.Utils;
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

        // CIA #1 TEMP DEBUG. TODO: Implement remaining CIA#1 registers such as Time Of Day
        c64mem.MapReader(0xdc08, Cia1DebugLoad);
        c64mem.MapWriter(0xdc08, Cia1DebugStore);
        c64mem.MapReader(0xdc09, Cia1DebugLoad);
        c64mem.MapWriter(0xdc09, Cia1DebugStore);
        c64mem.MapReader(0xdc0a, Cia1DebugLoad);
        c64mem.MapWriter(0xdc0a, Cia1DebugStore);
        c64mem.MapReader(0xdc0b, Cia1DebugLoad);
        c64mem.MapWriter(0xdc0b, Cia1DebugStore);


        // CIA #2 DataPort A
        // Address 0xdd00: "CIA 2 Port A" (VIC2 bank, serial bus, etc) actually belongs to CIA chip, but as it affects VIC2 bank selection it's added here
        c64mem.MapReader(CiaAddr.CIA2_DATAA, CIA2PortALoad);
        c64mem.MapWriter(CiaAddr.CIA2_DATAA, CIA2PortAStore);
        // TODO: Implement CIA #2 timers and registers
        //// CIA #2 Timer A
        //c64mem.MapReader(CiaAddr.CIA2_TIMAHI, Cia2TimerAHILoad);
        //c64mem.MapWriter(CiaAddr.CIA2_TIMAHI, Cia2TimerAHIStore);

        //c64mem.MapReader(CiaAddr.CIA2_TIMALO, Cia2TimerALOLoad);
        //c64mem.MapWriter(CiaAddr.CIA2_TIMALO, Cia2TimerALOStore);

        //// CIA #2 Timer B
        //c64mem.MapReader(CiaAddr.CIA2_TIMBHI, Cia2TimerBHILoad);
        //c64mem.MapWriter(CiaAddr.CIA2_TIMBHI, Cia2TimerBHIStore);

        //c64mem.MapReader(CiaAddr.CIA2_TIMBLO, Cia2TimerBLOLoad);
        //c64mem.MapWriter(CiaAddr.CIA2_TIMBLO, Cia2TimerBLOStore);

        // TODO: Implement CIA #2 timers and registers
        c64mem.MapReader(0xdd01, Cia2DebugLoad);
        c64mem.MapWriter(0xdd01, Cia2DebugStore);
        c64mem.MapReader(0xdd02, Cia2DebugLoad);
        c64mem.MapWriter(0xdd02, Cia2DebugStore);
        c64mem.MapReader(0xdd03, Cia2DebugLoad);
        c64mem.MapWriter(0xdd03, Cia2DebugStore);
        c64mem.MapReader(0xdd04, Cia2DebugLoad);
        c64mem.MapWriter(0xdd04, Cia2DebugStore);
        c64mem.MapReader(0xdd05, Cia2DebugLoad);
        c64mem.MapWriter(0xdd05, Cia2DebugStore);
        c64mem.MapReader(0xdd06, Cia2DebugLoad);
        c64mem.MapWriter(0xdd06, Cia2DebugStore);
        c64mem.MapReader(0xdd07, Cia2DebugLoad);
        c64mem.MapWriter(0xdd07, Cia2DebugStore);
        c64mem.MapReader(0xdd08, Cia2DebugLoad);
        c64mem.MapWriter(0xdd08, Cia2DebugStore);
        c64mem.MapReader(0xdd09, Cia2DebugLoad);
        c64mem.MapWriter(0xdd09, Cia2DebugStore);
        c64mem.MapReader(0xdd0a, Cia2DebugLoad);
        c64mem.MapWriter(0xdd0a, Cia2DebugStore);
        c64mem.MapReader(0xdd0b, Cia2DebugLoad);
        c64mem.MapWriter(0xdd0b, Cia2DebugStore);
    }

    // Cia 1 Data Port A is normally read from to get joystick (#2) input.
    // It's written to to control which keys can be read from Cia 1 Data Port B.
    public byte Cia1DataALoad(ushort _)
    {
        //// Temporary workaround, set the default state
        //byte value = 0x7f;

        var value = Keyboard.GetSelectedMatrixRow();

        // Also set Joystick #2 bits
        foreach (var action in Joystick.CurrentJoystickActions[2])
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
        foreach (var action in Joystick.CurrentJoystickActions[1])
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


    public byte Cia1DebugLoad(ushort address) => _c64.ReadIOStorage(address);
    public void Cia1DebugStore(ushort address, byte value) => _c64.WriteIOStorage(address, value);

    public void CIA2PortAStore(ushort address, byte value)
    {
        _c64.WriteIOStorage(address, value);

        // Note: CIA 2 Data Port A has two lines (bits) that control functionality in another chip, VIC2.
        //       Set VIC2 bank based on bits 0-1
        _c64.Vic2.SetVIC2Bank(value);

        // Handle serial bus lines.
        // Bit #3: Serial bus ATN OUT; 0 = High; 1 = Low.
        // Bit #4: Serial bus CLOCK OUT; 0 = High; 1 = Low.
        // Bit #5: Serial bus DATA OUT; 0 = High; 1 = Low.

        // If ATN/CLK/DATA bit is 1 means to hold the line "Low" (pulled) -> "true".
        _c64.IECBus.Host.SetLines(
            setATNLine: (value & (1 << 3)) != 0 ? DeviceLineState.Holding : DeviceLineState.NotHolding,
            setCLKLine: (value & (1 << 4)) != 0 ? DeviceLineState.Holding : DeviceLineState.NotHolding,
            setDATALine: (value & (1 << 5)) != 0 ? DeviceLineState.Holding : DeviceLineState.NotHolding
        );
    }

    public byte CIA2PortALoad(ushort address)
    {
        // CIA #2 Data Port A bit mapping:
        // Bits #0-#1: VIC bank. Values:
        //   %00, 0: Bank #3, $C000-$FFFF, 49152-65535.
        //   %01, 1: Bank #2, $8000-$BFFF, 32768-49151.
        //   %10, 2: Bank #1, $4000-$7FFF, 16384-32767.
        //   %11, 3: Bank #0, $0000-$3FFF, 0-16383.

        // Bit #2: RS232 TXD line, output bit.

        // Bit #3: Serial bus ATN OUT; 0 = High; 1 = Low.
        // Bit #4: Serial bus CLOCK OUT; 0 = High; 1 = Low.
        // Bit #5: Serial bus DATA OUT; 0 = High; 1 = Low.

        // Bit #6: Serial bus CLOCK IN; 0 = Low; 1 = High.
        // Bit #7: Serial bus DATA IN; 0 = Low; 1 = High.

        var value = _c64.ReadIOStorage(address);
        value &= 0b00111111; // Keep VIC2 bank selection bits only (bits 0-1) and last written serial port OUTPUT values (latched)

        // Get actual current serial port lines for CLOCK and DATA from bits 6-7 on the IEC bus.
        // The bit should be reported as 1 if the bus line is released (not pulled down) = "false" state.
        // Note: This is opposite of the device line state (output) bits 3,4,5.
        if (_c64.IECBus.CLKLineState == BusLineState.Released) value |= 1 << 6;
        if (_c64.IECBus.DATALineState == BusLineState.Released) value |= 1 << 7;
        return value;
    }

    // TODO: Implement CIA #2 timers and registers
    public byte Cia2TimerAHILoad(ushort _) => CiaTimers[CiaTimerType.Cia2_A].InternalTimer.Highbyte();
    public void Cia2TimerAHIStore(ushort _, byte value) => CiaTimers[CiaTimerType.Cia2_A].SetInternalTimer_Latch_HI(value);

    public byte Cia2TimerALOLoad(ushort _) => CiaTimers[CiaTimerType.Cia2_A].InternalTimer.Lowbyte();
    public void Cia2TimerALOStore(ushort _, byte value) => CiaTimers[CiaTimerType.Cia2_A].SetInternalTimer_Latch_LO(value);

    public byte Cia2TimerBHILoad(ushort _) => CiaTimers[CiaTimerType.Cia2_B].InternalTimer.Highbyte();
    public void Cia2TimerBHIStore(ushort _, byte value) => CiaTimers[CiaTimerType.Cia2_B].SetInternalTimer_Latch_HI(value);

    public byte Cia2TimerBLOLoad(ushort _) => CiaTimers[CiaTimerType.Cia2_B].InternalTimer.Lowbyte();
    public void Cia2TimerBLOStore(ushort _, byte value) => CiaTimers[CiaTimerType.Cia2_B].SetInternalTimer_Latch_LO(value);

    public byte Cia2DebugLoad(ushort address) => _c64.ReadIOStorage(address);
    public void Cia2DebugStore(ushort address, byte value) => _c64.WriteIOStorage(address, value);


}
