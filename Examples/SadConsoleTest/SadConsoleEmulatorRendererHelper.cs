using System;
using System.Collections.Generic;
using SadConsole;
using SadConsole.Effects;

namespace SadConsoleTest
{
    public static class SadConsoleEmulatorRendererHelper
    {
        // TODO: If different chatset in emulator, translate SadConsole charset?
        public static int GetSadConsoleCharCode(byte value)
        {
            switch (value)
            {
                default:
                    return (int)value;
            }
        }

        /// <summary>
        /// Returns a Xna color value based on the color information used in the emulator
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static Microsoft.Xna.Framework.Color ColorToXNAColor(byte color)
        {
            byte red;
            byte green;
            byte blue;

            // TODO: Currently maps to 64 color values. Should be configurable
            switch(color)
            {
                // Black
                case 0x00:
                    red = 0;
                    green = 0;
                    blue = 0;
                    break;
                // White
                case 0x01:
                    red = 255;
                    green = 255;
                    blue = 255;
                    break;                    
                // Red
                case 0x02:
                    red = 136;
                    green = 0;
                    blue = 0;
                    break;                    
                // Cyan
                case 0x03:
                    red = 170;
                    green = 255;
                    blue = 238;
                    break;                    
                // Violet/purple
                case 0x04:
                    red = 204;
                    green = 68;
                    blue = 204;
                    break;                    
                // Green
                case 0x05:
                    red = 0;
                    green = 204;
                    blue = 85;
                    break;                    
                // Blue
                case 0x06:
                    red = 0;
                    green = 0;
                    blue = 170;
                    break;
                // Yellow
                case 0x07:
                    red = 238;
                    green = 238;
                    blue = 119;
                    break;
                // Orange
                case 0x08:
                    red = 221;
                    green = 136;
                    blue = 185;
                    break;
                // Brown
                case 0x09:
                    red = 102;
                    green = 68;
                    blue = 0;
                    break;
                // Light red
                case 0x0a:
                    red = 255;
                    green = 119;
                    blue = 119;
                    break;
                // Dark grey
                case 0x0b:
                    red = 51;
                    green = 51;
                    blue = 51;
                    break;
                // Grey
                case 0x0c:
                    red = 119;
                    green = 119;
                    blue = 119;
                    break;
                // Light green
                case 0x0d:
                    red = 170;
                    green = 255;
                    blue = 102;
                    break;
                // Light blue
                case 0x0e:
                    red = 0;
                    green = 136;
                    blue = 255;
                    break;
                // Light grey
                case 0x0f:
                    red = 187;
                    green = 187;
                    blue = 187;
                    break;
                default:
                    red = 0xfF;
                    green = 0xff;
                    blue = 0xff;
                    break;
            }
            return new Microsoft.Xna.Framework.Color(red, green, blue);
        }

        public static Microsoft.Xna.Framework.Color AsXNAColor(this byte color) 
        {
            return ColorToXNAColor(color);
        }
    }
}
