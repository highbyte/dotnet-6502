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
    public const ushort COLOR_RAM_START = 0xd800 - 0xd000;

    // Sprite X/Y coordinates
    public const ushort SPRITE_0_X = 0xd000;
    public const ushort SPRITE_0_Y = 0xd001;
    public const ushort SPRITE_1_X = 0xd002;
    public const ushort SPRITE_1_Y = 0xd003;
    public const ushort SPRITE_2_X = 0xd004;
    public const ushort SPRITE_2_Y = 0xd005;
    public const ushort SPRITE_3_X = 0xd006;
    public const ushort SPRITE_3_Y = 0xd007;
    public const ushort SPRITE_4_X = 0xd008;
    public const ushort SPRITE_4_Y = 0xd009;
    public const ushort SPRITE_5_X = 0xd00a;
    public const ushort SPRITE_5_Y = 0xd00b;
    public const ushort SPRITE_6_X = 0xd00c;
    public const ushort SPRITE_6_Y = 0xd00d;
    public const ushort SPRITE_7_X = 0xd00e;
    public const ushort SPRITE_7_Y = 0xd00f;

    // Sprite X position MSB (Most Significant Bit) for sprites 0-7
    public const ushort SPRITE_MSB_X = 0xd010;

    public const ushort SCREEN_CONTROL_REGISTER_1 = 0xd011;
    public const ushort CURRENT_RASTER_LINE = 0xd012;

    public const ushort SPRITE_ENABLE = 0xd015;

    public const ushort SCROLL_X = 0xd016;

    public const ushort SPRITE_Y_EXPAND = 0xd017;

    public const ushort MEMORY_SETUP = 0xd018;

    public const ushort VIC_IRQ = 0xd019;
    public const ushort IRQ_MASK = 0xd01a;

    public const ushort SPRITE_FOREGROUND_PRIO = 0xd01b;
    public const ushort SPRITE_MULTICOLOR_ENABLE = 0xd01c;

    public const ushort SPRITE_X_EXPAND = 0xd01d;

    public const ushort BORDER_COLOR = 0xd020;
    public const ushort BACKGROUND_COLOR = 0xd021;

    public const ushort SPRITE_MULTI_COLOR_0 = 0xd025;  // Common for all sprites
    public const ushort SPRITE_MULTI_COLOR_1 = 0xd026;  // Common for all sprites

    public const ushort SPRITE_0_COLOR = 0xd027;
    public const ushort SPRITE_1_COLOR = 0xd028;
    public const ushort SPRITE_2_COLOR = 0xd029;
    public const ushort SPRITE_3_COLOR = 0xd02a;
    public const ushort SPRITE_4_COLOR = 0xd02b;
    public const ushort SPRITE_5_COLOR = 0xd02c;
    public const ushort SPRITE_6_COLOR = 0xd02d;
    public const ushort SPRITE_7_COLOR = 0xd02e;

    public const ushort PORT_A = 0xdd00;
}
