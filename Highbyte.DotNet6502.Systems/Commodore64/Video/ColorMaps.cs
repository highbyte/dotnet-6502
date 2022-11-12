using System.Drawing;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video
{
    public static class ColorMaps
    {
        /// <summary>
        /// Map C64 color value 0-15 to system RGB colors
        /// </summary>
        /// <returns></returns>
        private static Dictionary<byte, Color> C64ColorMap = new()
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

        public static Dictionary<byte, Color>.ValueCollection GetAllSystemColors()
        {
            return C64ColorMap.Values;
        }

        public static Color GetSystemColor(byte c64Color)
        {
            Color color;
            if (C64ColorMap.ContainsKey(c64Color))
                color = C64ColorMap[c64Color];
            else
                color = C64ColorMap.Values.First();
            return color;
        }
    }
}
