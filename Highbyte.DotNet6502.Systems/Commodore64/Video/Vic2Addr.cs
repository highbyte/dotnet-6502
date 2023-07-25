namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// VIC-II io chip addresses: 0xd000 - 0xd02e
/// Ref: https://github.com/mist64/c64ref/blob/master/Source/c64io/c64io_mapc64.txt
/// 
/// VIC-II screen ram (default): 0x0400 - 0x07e7 (40 columns and 25 rows, 1 byte per character = 0x03e8 (1000) bytes)
/// Ref: https://www.c64-wiki.com/wiki/Screen_RAM
/// Ref: https://www.pagetable.com/c64ref/c64mem/  (see Kernal routine use of 0x0288 and relation to VIC-II bank switching at 0xdd00 )
/// 
/// VIC-II color ram: 0xd800 - 0xdbe7 (one byte per character in screen ram = 0x03e8 (1000) bytes) 
/// Ref: https://www.c64-wiki.com/wiki/Color_RAM
/// </summary>
public static class Vic2Addr
{

    // TODO: Start of screen ram is configurable in VIC-II chip memory bank select at location $DD00 (56576).
    //       This should be variable that is calculated instead of a constant.
    public const ushort SCREEN_RAM_START = 0x0400;
    public const ushort COLOR_RAM_START = 0xd800;

    public const ushort SCREEN_CONTROL_REGISTER_1 = 0xd011;
    public const ushort CURRENT_RASTER_LINE = 0xd012;

    public const ushort MEMORY_SETUP = 0xd018;

    public const ushort VIC_IRQ = 0xd019;
    public const ushort IRQ_MASK = 0xd01a;

    public const ushort BORDER_COLOR = 0xd020;
    public const ushort BACKGROUND_COLOR = 0xd021;

    public const ushort PORT_A = 0xdd00;
}
