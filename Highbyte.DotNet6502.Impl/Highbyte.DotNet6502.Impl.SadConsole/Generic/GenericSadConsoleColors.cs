using System.Collections.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.Impl.SadConsole.Generic
{
    public static class GenericSadConsoleColors
    {
        // Reuse 
        public static Dictionary<System.Drawing.Color, SadRogue.Primitives.Color> NativeToSadConsoleColorMap = new();

        static GenericSadConsoleColors()
        {
            foreach (var nativeColor in ColorMaps.GenericColorMap.Values)
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
