using System.Collections.Generic;
using System.Linq;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64
{
    public static class C64SadConsoleColors
    {
        public static Dictionary<System.Drawing.Color, SadRogue.Primitives.Color> SystemToSadConsoleColorMap = new();

        static C64SadConsoleColors()
        {
            foreach (var systemColor in ColorMaps.GetAllSystemColors())
            {
                SystemToSadConsoleColorMap.Add(systemColor, systemColor.ToSadConsoleColor());
            }
        }

        public static SadRogue.Primitives.Color GetSadConsoleColor(System.Drawing.Color systemColorValue)
        {
            SadRogue.Primitives.Color color;
            if (!SystemToSadConsoleColorMap.ContainsKey(systemColorValue))
                color = SystemToSadConsoleColorMap.Values.First();
            else
                color = SystemToSadConsoleColorMap[systemColorValue];
            return color;
        }

        private static SadRogue.Primitives.Color ToSadConsoleColor(this System.Drawing.Color color)
        {
            return new Color(color.R, color.G, color.B, color.A);
        }
    }
}
