namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// CIA #1 io chip addresses: 0xdc00-0xdc0f
/// CIA #2 io chip addresses: 0xdd00-0xdd0f
/// Ref: https://github.com/mist64/c64ref/blob/master/Source/c64io/c64io_mapc64.txt
/// </summary>
public static class CiaAddr
{
    // --------------------
    // CIA #1
    // --------------------

    // CIA #1 Data Port A & B (Keyboard, Joystick)
    public const ushort CIA1_DATAA = 0xdc00;
    public const ushort CIA1_DATAB = 0xdc01;
    public const ushort CIA1_DDRA = 0xdc02;
    public const ushort CIA1_DDRB = 0xdc03;

    // CIA #1 Timer Register A
    public const ushort CIA1_TIMALO = 0xdc04;
    public const ushort CIA1_TIMAHI = 0xdc05;

    // CIA #1 Timer Register B
    public const ushort CIA1_TIMBLO = 0xdc06;
    public const ushort CIA1_TIMBHI = 0xdc07;

    // CIA #1 Time of Day Clock
    public const ushort CIA1_TOD10THS = 0xdc08;
    public const ushort CIA1_TODSEC = 0xdc09;
    public const ushort CIA1_TODMIN = 0xdc0a;
    public const ushort CIA1_TODHR = 0xdc0b;

    // CIA #1 Serial Data Register
    public const ushort CIA1_SDR = 0xdc0c;

    // CIA #1 Interrupt Control Register
    public const ushort CIA1_CIAICR = 0xdc0d;

    // CIA #1 Control Register A
    public const ushort CIA1_CIACRA = 0xdc0e;

    // CIA #1 Control Register B
    public const ushort CIA1_CIACRB = 0xdc0f;

    // --------------------
    // CIA #2
    // --------------------

    // CIA #2 Data Port A & B (VIC2 bank selection, serial bus, rs-232, user port)
    public const ushort CIA2_DATAA = 0xdd00;
    public const ushort CIA2_DATAB = 0xdd01;
    public const ushort CIA2_DDRA = 0xdd02;
    public const ushort CIA2_DDRB = 0xdd03;

    // CIA #2 Timer Register A
    public const ushort CIA2_TIMALO = 0xdd04;
    public const ushort CIA2_TIMAHI = 0xdd05;

    // CIA #2 Timer Register B
    public const ushort CIA2_TIMBLO = 0xdd06;
    public const ushort CIA2_TIMBHI = 0xdd07;

    // CIA #2 Time of Day Clock
    public const ushort CIA2_TOD10THS = 0xdd08;
    public const ushort CIA2_TODSEC = 0xdd09;
    public const ushort CIA2_TODMIN = 0xdd0a;
    public const ushort CIA2_TODHR = 0xdd0b;

    // CIA #2 Serial Data Register
    public const ushort CIA2_SDR = 0xdd0c;

    // CIA #2 Interrupt Control Register
    public const ushort CIA2_CIAICR = 0xdd0d;

    // CIA #2 Control Register A
    public const ushort CIA2_CIACRA = 0xdd0e;

    // CIA #2 Control Register B
    public const ushort CIA2_CIACRB = 0xdd0f;
}
