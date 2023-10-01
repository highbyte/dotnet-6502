using Highbyte.DotNet6502.Systems.Commodore64.Video;
using SkiaSharp;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video;

public class C64SkiaPaint
{
    /// <summary>
    /// C64 color to SKPaint object for solid fill.
    /// </summary>
    /// <returns></returns>
    private Dictionary<byte, SKPaint> _c64ToFillPaintMap = new();

    /// <summary>
    /// C64 color to SKPaint (with SkiaColorFilter) for replacing color when copying characters from chargen rom image to screen.
    /// </summary>
    /// <returns></returns>
    private Dictionary<byte, SKPaint> _c64ToDrawChargenCharacterMap = new();

    private Dictionary<(byte fg, byte bg), SKPaint> _c64ToDrawChargenCharacterMapWithBackground = new();

    /// <summary>
    /// C64 color to SKPaint  (with SkiaColorFilter) for copying generate sprite images to screen
    /// </summary>
    private Dictionary<byte, SKPaint> _c64ToDrawSpriteMap = new();

    public C64SkiaPaint(string colorMapName)
    {
        var c64SkiaColors = new C64SkiaColors(colorMapName);

        foreach (var c64Color in Enum.GetValues(typeof(C64Colors)))
        {
            var c64ColorValue = (byte)c64Color; // Color 0-15
            var systemColor = GetSystemColor(c64ColorValue, colorMapName); // .NET "Color" type
            var skColor = c64SkiaColors.SystemToSkColorMap[systemColor];    // Skia "SKColor" type
            var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = skColor };

            _c64ToFillPaintMap.Add(c64ColorValue, paint);
        }

        // Standard text mode, same background color for all characters
        foreach (var c64Color in Enum.GetValues(typeof(C64Colors)))
        {
            var c64ColorValue = (byte)c64Color; // Color 0-15
            var systemColor = GetSystemColor(c64ColorValue, colorMapName); // .NET "Color" type
            var skColor = c64SkiaColors.SystemToSkColorMap[systemColor];    // Skia "SKColor" type
            var colorFilter = CreateReplaceColorFilter(skColor, Chargen.CharacterImageDrawColor);
            var paint = new SKPaint { Style = SKPaintStyle.StrokeAndFill, ColorFilter = colorFilter };
            //var paint = new SKPaint { Style = SKPaintStyle.Stroke, ColorFilter = colorFilter };

            _c64ToDrawChargenCharacterMap.Add(c64ColorValue, paint);
        }

        foreach (var c64ColorFg in Enum.GetValues(typeof(C64Colors)))
        {
            foreach (var c64ColorBg in Enum.GetValues(typeof(C64Colors)))
            {
                var c64ColorValueFg = (byte)c64ColorFg; // Color 0-15
                var c64ColorValueBg = (byte)c64ColorBg; // Color 0-15

                var systemColorFg = GetSystemColor(c64ColorValueFg, colorMapName); // .NET "Color" type
                var systemColorBg = GetSystemColor(c64ColorValueBg, colorMapName); // .NET "Color" type

                var skColorFg = c64SkiaColors.SystemToSkColorMap[systemColorFg];    // Skia "SKColor" type
                var skColorBg = c64SkiaColors.SystemToSkColorMap[systemColorBg];    // Skia "SKColor" type

                var colorFilter = CreateReplaceColorFilter(skColorFg, Chargen.CharacterImageDrawColor, skColorBg);
                var paint = new SKPaint { Style = SKPaintStyle.StrokeAndFill, ColorFilter = colorFilter };
                //var paint = new SKPaint { Style = SKPaintStyle.Stroke, ColorFilter = colorFilter };

                _c64ToDrawChargenCharacterMapWithBackground.Add((c64ColorValueFg, c64ColorValueBg), paint);
            }
        }

        // Extended text mode, background color can be different for each character


        foreach (var c64Color in Enum.GetValues(typeof(C64Colors)))
        {
            var c64ColorValue = (byte)c64Color; // Color 0-15
            var systemColor = GetSystemColor(c64ColorValue, colorMapName); // .NET "Color" type
            var skColor = c64SkiaColors.SystemToSkColorMap[systemColor];    // Skia "SKColor" type
            var colorFilter = CreateReplaceColorFilter(skColor, SpriteGen.SpriteImageDrawColor);
            var paint = new SKPaint { Style = SKPaintStyle.StrokeAndFill, ColorFilter = colorFilter };
            //var paint = new SKPaint { Style = SKPaintStyle.Stroke, ColorFilter = colorFilter };

            _c64ToDrawSpriteMap.Add(c64ColorValue, paint);
        }
    }

    public SKPaint GetFillPaint(byte c64Color)
    {
        c64Color &= 0x0f; // Color range is only use lower 4 bits (values 0-15)
        return _c64ToFillPaintMap[c64Color];
    }

    public SKPaint GetDrawCharacterPaint(byte c64Color)
    {
        c64Color &= 0x0f; // Color range is only use lower 4 bits (values 0-15)
        return _c64ToDrawChargenCharacterMap[c64Color];
    }

    public SKPaint GetDrawCharacterPaintWithBackground(byte c64ColorFg, byte c64ColorBg)
    {
        c64ColorFg &= 0x0f; // Color range is only use lower 4 bits (values 0-15)
        c64ColorBg &= 0x0f; // Color range is only use lower 4 bits (values 0-15)
        return _c64ToDrawChargenCharacterMapWithBackground[(c64ColorFg, c64ColorBg)];
    }

    public SKPaint GetDrawSpritePaint(byte c64Color)
    {
        c64Color &= 0x0f; // Color range is only use lower 4 bits (values 0-15)
        return _c64ToDrawSpriteMap[c64Color];
    }

    /// <summary>
    /// Color filter change the color the original character image was drawn in to a specified color.
    /// </summary>
    /// <param name="newColor"></param>
    /// <param name="originalColor"></param>
    /// <returns></returns>
    private SKColorFilter CreateReplaceColorFilter(SKColor newColor, SKColor originalColor, SKColor? newBackgroundColor = null)
    {
        var R = new byte[256];
        var G = new byte[256];
        var B = new byte[256];
        var A = new byte[256];

        R[originalColor.Red] = newColor.Red;
        G[originalColor.Green] = newColor.Green;
        B[originalColor.Blue] = newColor.Blue;
        A[originalColor.Alpha] = newColor.Alpha;

        if (newBackgroundColor != null)
        {
            R[0] = newBackgroundColor.Value.Red;
            G[0] = newBackgroundColor.Value.Green;
            B[0] = newBackgroundColor.Value.Blue;
            A[0] = newBackgroundColor.Value.Alpha;
        }

        var colorFilter = SKColorFilter.CreateTable(A, R, G, B);
        return colorFilter;
    }
}
