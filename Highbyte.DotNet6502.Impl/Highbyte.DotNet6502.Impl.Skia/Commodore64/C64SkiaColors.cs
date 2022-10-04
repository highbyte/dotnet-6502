using System.Drawing;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64
{
    public static class C64SkiaColors
    {
        public static Dictionary<System.Drawing.Color, SKColor> NativeToSkColorMap = new();

        static C64SkiaColors()
        {
            foreach (var nativeColor in ColorMaps.C64ColorMap.Values)
            {
                NativeToSkColorMap.Add(nativeColor, nativeColor.ToSkColor());
            }
        }

        private static SKColor ToSkColor(this Color color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }
    }
}