using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// CIA #2 (Complex Interface Adapter) chip implementation.
/// Handles VIC-II bank selection, serial bus (IEC) communication, RS-232, user port functionality, and general-purpose timers.
/// Located at memory addresses 0xDD00-0xDD0F.
/// </summary>
public class Cia2 : CiaBase
{
    public Cia2(C64 c64, ILoggerFactory loggerFactory)
        : base(c64, new CiaIRQ(useNMI: true)) // Initialize CIA #1 timer with to raise NMI instead of IRQ
    {
    }

    public override void MapIOLocations(Memory c64mem)
    {
        // CIA #2 DataPort A (VIC2 bank, serial bus, etc)
        c64mem.MapReader(CiaAddr.CIA2_DATAA, DataALoad);
        c64mem.MapWriter(CiaAddr.CIA2_DATAA, DataAStore);

        // CIA #2 DataPort B 
        c64mem.MapReader(CiaAddr.CIA2_DATAB, DataBLoad);
        c64mem.MapWriter(CiaAddr.CIA2_DATAB, DataBStore);

        // CIA #2 Timer A (using base class methods)
        c64mem.MapReader(CiaAddr.CIA2_TIMAHI, TimerAHILoad);
        c64mem.MapWriter(CiaAddr.CIA2_TIMAHI, TimerAHIStore);

        c64mem.MapReader(CiaAddr.CIA2_TIMALO, TimerALOLoad);
        c64mem.MapWriter(CiaAddr.CIA2_TIMALO, TimerALOStore);

        // CIA #2 Timer B (using base class methods)
        c64mem.MapReader(CiaAddr.CIA2_TIMBHI, TimerBHILoad);
        c64mem.MapWriter(CiaAddr.CIA2_TIMBHI, TimerBHIStore);

        c64mem.MapReader(CiaAddr.CIA2_TIMBLO, TimerBLOLoad);
        c64mem.MapWriter(CiaAddr.CIA2_TIMBLO, TimerBLOStore);

        // CIA #2 Interrupt Control Register (using base class methods)
        c64mem.MapReader(CiaAddr.CIA2_CIAICR, InterruptControlLoad);
        c64mem.MapWriter(CiaAddr.CIA2_CIAICR, InterruptControlStore);

        // CIA #2 Control Register A (using base class methods)
        c64mem.MapReader(CiaAddr.CIA2_CIACRA, TimerAControlLoad);
        c64mem.MapWriter(CiaAddr.CIA2_CIACRA, TimerAControlStore);

        // CIA #2 Control Register B (using base class methods)
        c64mem.MapReader(CiaAddr.CIA2_CIACRB, TimerBControlLoad);
        c64mem.MapWriter(CiaAddr.CIA2_CIACRB, TimerBControlStore);

        // CIA #2 Time Of Day registers (temporary debug implementation)
        c64mem.MapReader(CiaAddr.CIA2_TOD10THS, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA2_TOD10THS, DebugStore);
        c64mem.MapReader(CiaAddr.CIA2_TODSEC, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA2_TODSEC, DebugStore);
        c64mem.MapReader(CiaAddr.CIA2_TODMIN, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA2_TODMIN, DebugStore);
        c64mem.MapReader(CiaAddr.CIA2_TODHR, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA2_TODHR, DebugStore);

        // CIA #2 Serial Data Register (temporary debug implementation)
        c64mem.MapReader(CiaAddr.CIA2_SDR, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA2_SDR, DebugStore);

        // CIA #2 Data Direction Registers (temporary debug implementation)
        c64mem.MapReader(CiaAddr.CIA2_DDRA, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA2_DDRA, DebugStore);
        c64mem.MapReader(CiaAddr.CIA2_DDRB, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA2_DDRB, DebugStore);
    }

    /// <summary>
    /// CIA #2 Data Port A controls VIC-II bank selection and serial bus (IEC) communication.
    /// </summary>
    public byte DataALoad(ushort address)
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

    /// <summary>
    /// Writing to CIA #2 Data Port A controls VIC-II bank and serial bus lines.
    /// </summary>
    public void DataAStore(ushort address, byte value)
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

    /// <summary>
    /// CIA #2 Data Port B handles user port and RS-232 functionality.
    /// </summary>
    public byte DataBLoad(ushort address)
    {
        // TODO: Implement user port and RS-232 functionality
        return DebugLoad(address);
    }

    /// <summary>
    /// Writing to CIA #2 Data Port B controls user port and RS-232 functionality.
    /// </summary>
    public void DataBStore(ushort address, byte value)
    {
        // TODO: Implement user port and RS-232 functionality
        DebugStore(address, value);
    }
}
