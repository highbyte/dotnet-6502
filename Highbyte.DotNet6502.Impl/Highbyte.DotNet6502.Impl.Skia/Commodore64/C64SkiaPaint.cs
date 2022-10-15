using Highbyte.DotNet6502.Systems.Commodore64.Config;
using SkiaSharp;
using static Highbyte.DotNet6502.Systems.Commodore64.Config.ColorMaps;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64
{
    public static class C64SkiaPaint
    {
        /// <summary>
        /// C64 color to SKPaint object for solid fill.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<byte, SKPaint> C64ToFillPaintMap = new();

        /// <summary>
        /// C64 color to SKPaint (with SkiaColorFilter) for replacing color when copying characters from chargen rom image to screen.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<byte, SKPaint> C64ToDrawChargenCharecterMap = new();

        static C64SkiaPaint()
        {
            foreach (var c64Color in Enum.GetValues(typeof(C64Colors)))
            {
                var c64ColorValue = (byte)c64Color; // Color 0-15
                var systemColor = ColorMaps.C64ColorMap[c64ColorValue]; // .NET "Color" type
                var skColor = C64SkiaColors.SystemToSkColorMap[systemColor];    // Skia "SKColor" type
                var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = skColor };

                C64ToFillPaintMap.Add(c64ColorValue, paint);
            }

            foreach (var c64Color in Enum.GetValues(typeof(C64Colors)))
            {
                var c64ColorValue = (byte)c64Color; // Color 0-15
                var systemColor = ColorMaps.C64ColorMap[c64ColorValue]; // .NET "Color" type
                var skColor = C64SkiaColors.SystemToSkColorMap[systemColor];    // Skia "SKColor" type
                var colorFilter = CreateReplaceColorFilter(skColor, Chargen.CharacterImageDrawColor);
                var paint = new SKPaint { Style = SKPaintStyle.StrokeAndFill, ColorFilter = colorFilter };
                //var paint = new SKPaint { Style = SKPaintStyle.Stroke, ColorFilter = colorFilter };

                C64ToDrawChargenCharecterMap.Add(c64ColorValue, paint);
            }
        }

        /// <summary>
        /// Color filter change the color the original character image was drawn in to a specified color.
        /// </summary>
        /// <param name="newColor"></param>
        /// <param name="originalColor"></param>
        /// <returns></returns>
        private static SKColorFilter CreateReplaceColorFilter(SKColor newColor, SKColor originalColor)
        {
            byte[] R = new byte[256];
            byte[] G = new byte[256];
            byte[] B = new byte[256];
            byte[] A = new byte[256];

            R[originalColor.Red] = newColor.Red;
            G[originalColor.Green] = newColor.Green;
            B[originalColor.Blue] = newColor.Blue;
            A[originalColor.Alpha] = newColor.Alpha;

            var colorFilter = SKColorFilter.CreateTable(A, R, G, B);
            return colorFilter;
        }
    }
}