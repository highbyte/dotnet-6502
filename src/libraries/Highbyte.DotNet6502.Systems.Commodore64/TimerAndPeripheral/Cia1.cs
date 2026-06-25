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
    private byte _portA = 0xFF;
    private byte _portB = 0xFF;
    private byte _ddra;
    private byte _ddrb;

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
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_DATAA, DataALoad, DataAStore);

        // CIA #1 DataPort B
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_DATAB, DataBLoad, DataBStore);

        // CIA #1 Timer A (using base class methods)
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_TIMAHI, TimerAHILoad, TimerAHIStore);

        MapRegisterMirrors(c64mem, CiaAddr.CIA1_TIMALO, TimerALOLoad, TimerALOStore);

        // CIA #1 Timer B (using base class methods)
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_TIMBHI, TimerBHILoad, TimerBHIStore);

        MapRegisterMirrors(c64mem, CiaAddr.CIA1_TIMBLO, TimerBLOLoad, TimerBLOStore);

        // CIA #1 Interrupt Control Register (using base class methods)
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_CIAICR, InterruptControlLoad, InterruptControlStore);

        // CIA #1 Control Register A (using base class methods)
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_CIACRA, TimerAControlLoad, TimerAControlStore);

        // CIA #1 Control Register B (using base class methods)
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_CIACRB, TimerBControlLoad, TimerBControlStore);

        // CIA #1 Time Of Day registers (temporary debug implementation)
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_TOD10THS, DebugLoad, DebugStore);
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_TODSEC, DebugLoad, DebugStore);
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_TODMIN, DebugLoad, DebugStore);
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_TODHR, DebugLoad, DebugStore);

        // CIA #1 Serial Data Register (temporary debug implementation)
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_SDR, DebugLoad, DebugStore);

        // CIA #1 Data Direction Registers
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_DDRA, DDRARead, DDRAWrite);
        MapRegisterMirrors(c64mem, CiaAddr.CIA1_DDRB, DDRBRead, DDRBWrite);
    }

    /// <summary>
    /// CIA 1 Data Port A is normally read from to get joystick (#2) input.
    /// It's written to to control which keys can be read from CIA 1 Data Port B.
    /// </summary>
    public byte DataALoad(ushort _)
    {
        var inputValue = Keyboard.GetPortAInput(GetDrivenMask(_portB, _ddrb));
        var result = ComposePortReadValue(_portA, _ddra, inputValue);

        // Joystick port 2 pins are shared with Port A output pins. The joystick can pull a pin
        // low regardless of the DDR direction, so bits are cleared on the composed result rather
        // than on inputValue (which ComposePortReadValue ignores for output-configured bits).
        foreach (var action in Joystick.CurrentJoystickActions[2])
            result.ClearBit((int)action);

        return result;
    }

    /// <summary>
    /// Writing to CIA 1 Data Port A controls which keys can be read from CIA 1 Data Port B.
    /// </summary>
    public void DataAStore(ushort address, byte value)
    {
        _portA = value;
        _c64.WriteIOStorage(address, value);
    }

    /// <summary>
    /// When reading from CIA 1 Data Port B you can get both keyboard and joystick (#1) input sharing the same bits (which can be confusing).
    /// </summary>
    public byte DataBLoad(ushort address)
    {
        var inputValue = Keyboard.GetPortBInput(GetDrivenMask(_portA, _ddra));
        var result = ComposePortReadValue(_portB, _ddrb, inputValue);

        // Joystick port 1 pins are shared with Port B. Same reasoning as DataALoad: joystick can
        // pull a pin low regardless of DDR direction, so bits are cleared on the composed result.
        foreach (var action in Joystick.CurrentJoystickActions[1])
            result.ClearBit((int)action);

        return result;
    }

    public void DataBStore(ushort address, byte value)
    {
        _portB = value;
        _c64.WriteIOStorage(address, value);
    }

    public byte DDRARead(ushort _) => _ddra;
    public void DDRAWrite(ushort address, byte value)
    {
        _ddra = value;
        _c64.WriteIOStorage(address, value);
    }

    public byte DDRBRead(ushort _) => _ddrb;
    public void DDRBWrite(ushort address, byte value)
    {
        _ddrb = value;
        _c64.WriteIOStorage(address, value);
    }

    private static byte ComposePortReadValue(byte outputRegister, byte dataDirectionRegister, byte inputValue)
    {
        return (byte)((outputRegister & dataDirectionRegister) | (inputValue & ~dataDirectionRegister));
    }

    private static byte GetDrivenMask(byte outputRegister, byte dataDirectionRegister)
    {
        return (byte)(outputRegister | ~dataDirectionRegister);
    }
}
