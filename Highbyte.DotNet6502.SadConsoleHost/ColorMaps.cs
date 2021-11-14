using SadRogue.Primitives;
using System.Collections.Generic;

namespace Highbyte.DotNet6502.SadConsoleHost
{
    public static class ColorMaps
    {
        public static Dictionary<byte, Color> C64ColorMap = new()
        {
            { 0x00, new Color(0, 0, 0) },          // Black
            { 0x01, new Color(255, 255, 255) },    // White
            { 0x02, new Color(136, 0, 0) },        // Red
            { 0x03, new Color(170, 255, 238) },    // Cyan
            { 0x04, new Color(204, 68, 204) },     // Violet/purple
            { 0x05, new Color(0, 204, 85) },       // Green
            { 0x06, new Color(0, 0, 170) },        // Blue
            { 0x07, new Color(238, 238, 119) },    // Yellow
            { 0x08, new Color(221, 136, 185) },    // Orange
            { 0x09, new Color(102, 68, 0) },       // Brown
            { 0x0a, new Color(255, 119, 119) },    // Light red
            { 0x0b, new Color(51, 51, 51) },       // Dark grey
            { 0x0c, new Color(119, 119, 119) },    // Grey
            { 0x0d, new Color(170, 255, 102) },    // Light green
            { 0x0e, new Color(0, 136, 255) },      // Light blue
            { 0x0f, new Color(187, 187, 187) },    // Light grey
        };
    }
}
