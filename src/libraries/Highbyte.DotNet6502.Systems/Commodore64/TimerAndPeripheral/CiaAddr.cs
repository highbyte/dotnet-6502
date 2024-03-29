namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// CIA #1 io chip addresses: 0xdc00-0xdc0f
/// CIA #2 io chip addresses: 0xdd00-0xdd0f
/// Ref: https://github.com/mist64/c64ref/blob/master/Source/c64io/c64io_mapc64.txt
/// </summary>
public static class CiaAddr
{
    // CIA #1 Data Port A & B (Keyboard, Joystick)
    public const ushort CIA1_DATAA = 0xdc00;
    public const ushort CIA1_DATAB = 0xdc01;

    // CIA #1 Timer Register A
    public const ushort CIA1_TIMALO = 0xdc04;
    public const ushort CIA1_TIMAHI = 0xdc05;

    // CIA #1 Timer Register B
    public const ushort CIA1_TIMBLO = 0xdc06;
    public const ushort CIA1_TIMBHI = 0xdc07;

    // CIA #1 Interrupt Control Register
    public const ushort CIA1_CIAICR = 0xdc0d;

    // CIA #1 Control Register A
    public const ushort CIA1_CIACRA = 0xdc0e;

    // CIA #1 Control Register B
    public const ushort CIA1_CIACRB = 0xdc0f;


    // CIA #2 Data Port A & B (VIC2 bank selection, serial bus, rs-232, user port)
    public const ushort CIA2_DATAA = 0xdd00;
    public const ushort CIA2_DATAB = 0xdd01;

    // CIA #2 Timer Register A
    public const ushort CIA2_TIMALO = 0xdd04;
    public const ushort CIA2_TIMAHI = 0xdd05;

    // CIA #2 Timer Register B
    public const ushort CIA2_TIMBLO = 0xdd06;
    public const ushort CIA2_TIMBHI = 0xdd07;

}
