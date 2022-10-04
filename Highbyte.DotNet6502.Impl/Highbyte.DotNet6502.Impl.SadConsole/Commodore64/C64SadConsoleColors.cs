using System.Collections.Generic;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64
{
    public static class C64SadConsoleColors
    {
        public static Dictionary<System.Drawing.Color, SadRogue.Primitives.Color> NativeToSadConsoleColorMap = new();

        static C64SadConsoleColors()
        {
            foreach (var nativeColor in ColorMaps.C64ColorMap.Values)
            {
                NativeToSadConsoleColorMap.Add(nativeColor, nativeColor.ToSadConsoleColor());
            }
        }

        public static SadRogue.Primitives.Color ToSadConsoleColor(this System.Drawing.Color color)
        {
            return new Color(color.R, color.G, color.B, color.A);
        }
    }
}