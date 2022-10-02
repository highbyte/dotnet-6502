using System.Collections.Generic;
using System.Drawing;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config
{
    public static class ColorMaps
    {
        public static Dictionary<byte, Color> C64ColorMap = new()
        {
            { 0x00, Color.FromArgb(0, 0, 0) },          // Black
            { 0x01, Color.FromArgb(255, 255, 255) },    // White
            { 0x02, Color.FromArgb(136, 0, 0) },        // Red
            { 0x03, Color.FromArgb(170, 255, 238) },    // Cyan
            { 0x04, Color.FromArgb(204, 68, 204) },     // Violet/purple
            { 0x05, Color.FromArgb(0, 204, 85) },       // Green
            { 0x06, Color.FromArgb(0, 0, 170) },        // Blue
            { 0x07, Color.FromArgb(238, 238, 119) },    // Yellow
            { 0x08, Color.FromArgb(221, 136, 185) },    // Orange
            { 0x09, Color.FromArgb(102, 68, 0) },       // Brown
            { 0x0a, Color.FromArgb(255, 119, 119) },    // Light red
            { 0x0b, Color.FromArgb(51, 51, 51) },       // Dark grey
            { 0x0c, Color.FromArgb(119, 119, 119) },    // Grey
            { 0x0d, Color.FromArgb(170, 255, 102) },    // Light green
            { 0x0e, Color.FromArgb(0, 136, 255) },      // Light blue
            { 0x0f, Color.FromArgb(187, 187, 187) },    // Light grey
        };
    }
}
