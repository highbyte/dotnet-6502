using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// CIA #1 (Complex Interface Adapter) chip implementation.
/// Handles keyboard matrix scanning, joystick input, and general-purpose timers.
/// Located at memory addresses 0xDC00-0xDC0F.
/// </summary>
public class Cia1 : CiaBase
{
    public C64Keyboard Keyboard { get; private set; }
    public C64Joystick Joystick { get; private set; }

    public Cia1(C64 c64, Config.C64Config c64Config, ILoggerFactory loggerFactory) 
        : base(c64, new CiaIRQ(useNMI: false)) // Initialize CIA #1 timer with to raise IRQ instead of NMI
    {
        Keyboard = new C64Keyboard(c64, loggerFactory);
        Joystick = new C64Joystick(c64Config, loggerFactory);
    }

    public override void MapIOLocations(Memory c64mem)
    {
        // CIA #1 DataPort A
        c64mem.MapReader(CiaAddr.CIA1_DATAA, DataALoad);
        c64mem.MapWriter(CiaAddr.CIA1_DATAA, DataAStore);

        // CIA #1 DataPort B
        c64mem.MapReader(CiaAddr.CIA1_DATAB, DataBLoad);
        c64mem.MapWriter(CiaAddr.CIA1_DATAB, DataBStore);

        // CIA #1 Timer A (using base class methods)
        c64mem.MapReader(CiaAddr.CIA1_TIMAHI, TimerAHILoad);
        c64mem.MapWriter(CiaAddr.CIA1_TIMAHI, TimerAHIStore);

        c64mem.MapReader(CiaAddr.CIA1_TIMALO, TimerALOLoad);
        c64mem.MapWriter(CiaAddr.CIA1_TIMALO, TimerALOStore);

        // CIA #1 Timer B (using base class methods)
        c64mem.MapReader(CiaAddr.CIA1_TIMBHI, TimerBHILoad);
        c64mem.MapWriter(CiaAddr.CIA1_TIMBHI, TimerBHIStore);

        c64mem.MapReader(CiaAddr.CIA1_TIMBLO, TimerBLOLoad);
        c64mem.MapWriter(CiaAddr.CIA1_TIMBLO, TimerBLOStore);

        // CIA #1 Interrupt Control Register (using base class methods)
        c64mem.MapReader(CiaAddr.CIA1_CIAICR, InterruptControlLoad);
        c64mem.MapWriter(CiaAddr.CIA1_CIAICR, InterruptControlStore);

        // CIA #1 Control Register A (using base class methods)
        c64mem.MapReader(CiaAddr.CIA1_CIACRA, TimerAControlLoad);
        c64mem.MapWriter(CiaAddr.CIA1_CIACRA, TimerAControlStore);

        // CIA #1 Control Register B (using base class methods)
        c64mem.MapReader(CiaAddr.CIA1_CIACRB, TimerBControlLoad);
        c64mem.MapWriter(CiaAddr.CIA1_CIACRB, TimerBControlStore);

        // CIA #1 Time Of Day registers (temporary debug implementation)
        c64mem.MapReader(CiaAddr.CIA1_TOD10THS, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA1_TOD10THS, DebugStore);
        c64mem.MapReader(CiaAddr.CIA1_TODSEC, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA1_TODSEC, DebugStore);
        c64mem.MapReader(CiaAddr.CIA1_TODMIN, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA1_TODMIN, DebugStore);
        c64mem.MapReader(CiaAddr.CIA1_TODHR, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA1_TODHR, DebugStore);

        // CIA #1 Serial Data Register (temporary debug implementation)
        c64mem.MapReader(CiaAddr.CIA1_SDR, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA1_SDR, DebugStore);

        // CIA #1 Data Direction Registers (temporary debug implementation)
        c64mem.MapReader(CiaAddr.CIA1_DDRA, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA1_DDRA, DebugStore);
        c64mem.MapReader(CiaAddr.CIA1_DDRB, DebugLoad);
        c64mem.MapWriter(CiaAddr.CIA1_DDRB, DebugStore);
    }

    /// <summary>
    /// CIA 1 Data Port A is normally read from to get joystick (#2) input.
    /// It's written to to control which keys can be read from CIA 1 Data Port B.
    /// </summary>
    public byte DataALoad(ushort _)
    {
        var value = Keyboard.GetSelectedMatrixRow();

        // Also set Joystick #2 bits
        foreach (var action in Joystick.CurrentJoystickActions[2])
        {
            value.ClearBit((int)action);
        }

        return value;
    }

    /// <summary>
    /// Writing to CIA 1 Data Port A controls which keys can be read from CIA 1 Data Port B.
    /// </summary>
    public void DataAStore(ushort address, byte value)
    {
        Keyboard.SetSelectedMatrixRow(value);
    }

    /// <summary>
    /// When reading from CIA 1 Data Port B you can get both keyboard and joystick (#1) input sharing the same bits (which can be confusing).
    /// </summary>
    public byte DataBLoad(ushort address)
    {
        // Get the pressed keys for the selected matrix row (set by writing to CIA 1 Data Port A DC00)
        var value = Keyboard.GetPressedKeysForSelectedMatrixRow();

        // Also set Joystick #1 bits
        foreach (var action in Joystick.CurrentJoystickActions[1])
        {
            value.ClearBit((int)action);
        }
        return value;
    }

    public void DataBStore(ushort address, byte value)
    {
        // TODO: What will writing to this address affect?
        _c64.WriteIOStorage(address, value);
    }
}
