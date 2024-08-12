using System.Drawing;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

public static class ColorMaps
{
    public const string DEFAULT_COLOR_MAP_NAME = "Default";

    private static readonly Dictionary<string, Dictionary<byte, Color>> s_colorMaps = new();

    public enum C64Colors : byte
    {
        Black = 0x00,
        White = 0x01,
        Red = 0x02,
        Cyan = 0x03,
        Violet = 0x04,
        Green = 0x05,
        Blue = 0x06,
        Yellow = 0x07,
        Orange = 0x08,
        Brown = 0x09,
        LightRed = 0x0a,
        DarkGrey = 0x0b,
        Grey = 0x0c,
        LightGreen = 0x0d,
        LightBlue = 0x0e,
        LightGrey = 0x0f
    }

    static ColorMaps()
    {
        // Add color maps
        s_colorMaps.Add(DEFAULT_COLOR_MAP_NAME, C64ColorMap_Godot);
        s_colorMaps.Add("C64HQ", C64ColorMap_C64HQ);
        s_colorMaps.Add("Godot", C64ColorMap_Godot);
        s_colorMaps.Add("Old", C64ColorMap_OLD);
    }

    /// <summary>
    /// Same as Vice 64 emulator "C64HQ" palette option
    /// </summary>
    private static Dictionary<byte, Color> C64ColorMap_C64HQ = new()
    {
        { (byte)C64Colors.Black,        Color.FromArgb(0x00, 0x00, 0x00) }, // Black
        { (byte)C64Colors.White,        Color.FromArgb(0xff, 0xf8, 0xff) }, // White
        { (byte)C64Colors.Red,          Color.FromArgb(0x8f, 0x1f, 0x02) }, // Red
        { (byte)C64Colors.Cyan,         Color.FromArgb(0x65, 0xcd, 0xa8) }, // Cyan
        { (byte)C64Colors.Violet,       Color.FromArgb(0xa7, 0x3b, 0x9f) }, // Violet/purple
        { (byte)C64Colors.Green,        Color.FromArgb(0x4d, 0xab, 0x19) }, // Green
        { (byte)C64Colors.Blue,         Color.FromArgb(0x1a, 0x0c, 0x92) }, // Blue
        { (byte)C64Colors.Yellow,       Color.FromArgb(0xeb, 0xe3, 0x53) }, // Yellow
        { (byte)C64Colors.Orange,       Color.FromArgb(0xa9, 0x4b, 0x02) }, // Orange
        { (byte)C64Colors.Brown,        Color.FromArgb(0x44, 0x1e, 0x00) }, // Brown
        { (byte)C64Colors.LightRed,     Color.FromArgb(0xd2, 0x80, 0x74) }, // Light red
        { (byte)C64Colors.DarkGrey,     Color.FromArgb(0x46, 0x46, 0x46) }, // Dark grey
        { (byte)C64Colors.Grey,         Color.FromArgb(0x8b, 0x8b, 0x8b) }, // Grey
        { (byte)C64Colors.LightGreen,   Color.FromArgb(0x8e, 0xf6, 0x8e) }, // Light green
        { (byte)C64Colors.LightBlue,    Color.FromArgb(0x4d, 0x91, 0xd1) }, // Light blue
        { (byte)C64Colors.LightGrey,    Color.FromArgb(0xba, 0xba, 0xba) }, // Light grey
    };

    /// <summary>
    /// Same as Vice 64 emulator "Godot" palette option
    /// </summary>
    private static Dictionary<byte, Color> C64ColorMap_Godot = new()
    {
        { (byte)C64Colors.Black,        Color.FromArgb(0x00, 0x00, 0x00) }, // Black
        { (byte)C64Colors.White,        Color.FromArgb(0xff, 0xff, 0xff) }, // White
        { (byte)C64Colors.Red,          Color.FromArgb(0x88, 0x00, 0x00) }, // Red
        { (byte)C64Colors.Cyan,         Color.FromArgb(0xaa, 0xff, 0xee) }, // Cyan
        { (byte)C64Colors.Violet,       Color.FromArgb(0xcc, 0x44, 0xcc) }, // Violet/purple
        { (byte)C64Colors.Green,        Color.FromArgb(0x00, 0xcc, 0x55) }, // Green
        { (byte)C64Colors.Blue,         Color.FromArgb(0x00, 0x00, 0xaa) }, // Blue
        { (byte)C64Colors.Yellow,       Color.FromArgb(0xee, 0xee, 0x77) }, // Yellow
        { (byte)C64Colors.Orange,       Color.FromArgb(0xdd, 0x88, 0x55) }, // Orange
        { (byte)C64Colors.Brown,        Color.FromArgb(0x66, 0x44, 0x00) }, // Brown
        { (byte)C64Colors.LightRed,     Color.FromArgb(0xfe, 0x77, 0x77) }, // Light red
        { (byte)C64Colors.DarkGrey,     Color.FromArgb(0x33, 0x33, 0x33) }, // Dark grey
        { (byte)C64Colors.Grey,         Color.FromArgb(0x77, 0x77, 0x77) }, // Grey
        { (byte)C64Colors.LightGreen,   Color.FromArgb(0xaa, 0xff, 0x66) }, // Light green
        { (byte)C64Colors.LightBlue,    Color.FromArgb(0x00, 0x88, 0xff) }, // Light blue
        { (byte)C64Colors.LightGrey,    Color.FromArgb(0xbb, 0xbb, 0xbb) }, // Light grey
    };

    /// <summary>
    /// Map C64 color value 0-15 to system RGB colors
    /// </summary>
    /// <returns></returns>
    private static Dictionary<byte, Color> C64ColorMap_OLD = new()
    {
        { (byte)C64Colors.Black,        Color.FromArgb(0, 0, 0) },          // Black
        { (byte)C64Colors.White,        Color.FromArgb(255, 255, 255) },    // White
        { (byte)C64Colors.Red,          Color.FromArgb(136, 0, 0) },        // Red
        { (byte)C64Colors.Cyan,         Color.FromArgb(170, 255, 238) },    // Cyan
        { (byte)C64Colors.Violet,       Color.FromArgb(204, 68, 204) },     // Violet/purple
        { (byte)C64Colors.Green,        Color.FromArgb(0, 204, 85) },       // Green
        { (byte)C64Colors.Blue,         Color.FromArgb(0, 0, 170) },        // Blue
        { (byte)C64Colors.Yellow,       Color.FromArgb(238, 238, 119) },    // Yellow
        { (byte)C64Colors.Orange,       Color.FromArgb(221, 136, 185) },    // Orange
        { (byte)C64Colors.Brown,        Color.FromArgb(102, 68, 0) },       // Brown
        { (byte)C64Colors.LightRed,     Color.FromArgb(255, 119, 119) },    // Light red
        { (byte)C64Colors.DarkGrey,     Color.FromArgb(51, 51, 51) },       // Dark grey
        { (byte)C64Colors.Grey,         Color.FromArgb(119, 119, 119) },    // Grey
        { (byte)C64Colors.LightGreen,   Color.FromArgb(170, 255, 102) },    // Light green
        { (byte)C64Colors.LightBlue,    Color.FromArgb(0, 136, 255) },      // Light blue
        { (byte)C64Colors.LightGrey,    Color.FromArgb(187, 187, 187) },    // Light grey
    };

    public static Dictionary<byte, Color>.ValueCollection GetAllSystemColors(string colorMapName)
    {
        return s_colorMaps[colorMapName].Values;
    }

    public static Color GetSystemColor(byte c64Color, string colorMapName)
    {
        c64Color &= 0x0f; // Color range is only use lower 4 bits (values 0-15)

        Color color;
        if (s_colorMaps[colorMapName].ContainsKey(c64Color))
            color = s_colorMaps[colorMapName][c64Color];
        else
            color = s_colorMaps[colorMapName].Values.First();
        return color;
    }
}
