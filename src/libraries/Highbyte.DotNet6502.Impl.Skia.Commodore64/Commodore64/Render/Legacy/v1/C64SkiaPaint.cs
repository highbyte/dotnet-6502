using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Render.Legacy.v1;

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
    private Dictionary<(byte color, byte multiColorBG1, byte multiColorBG2), SKPaint> _c64ToDrawChargenCharacterMapWithMultiColor = new();

    /// <summary>
    /// C64 color to SKPaint  (with SkiaColorFilter) for copying generate sprite images to screen
    /// </summary>
    private Dictionary<byte, SKPaint> _c64ToDrawSpriteMap = new();

    public C64SkiaPaint(string colorMapName)
    {
        var c64SkiaColors = new C64SkiaColors(colorMapName);

        foreach (var c64Color in Enum.GetValues<C64Colors>())
        {
            var c64ColorValue = (byte)c64Color; // Color 0-15
            var systemColor = GetSystemColor(c64ColorValue, colorMapName); // .NET "Color" type
            var skColor = c64SkiaColors.SystemToSkColorMap[systemColor];    // Skia "SKColor" type
            var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = skColor };

            _c64ToFillPaintMap.Add(c64ColorValue, paint);
        }

        // Standard text mode, same background color for all characters
        foreach (var c64Color in Enum.GetValues<C64Colors>())
        {
            var c64ColorValue = (byte)c64Color; // Color 0-15
            var systemColor = GetSystemColor(c64ColorValue, colorMapName); // .NET "Color" type
            var skColor = c64SkiaColors.SystemToSkColorMap[systemColor];    // Skia "SKColor" type
            var colorFilter = CreateReplaceColorFilter(skColor, CharGen.CharacterImageDrawColor);
            var paint = new SKPaint { Style = SKPaintStyle.StrokeAndFill, ColorFilter = colorFilter };
            //var paint = new SKPaint { Style = SKPaintStyle.Stroke, ColorFilter = colorFilter };

            _c64ToDrawChargenCharacterMap.Add(c64ColorValue, paint);
        }

        // Extended text mode, background color can be different for each character
        foreach (var c64ColorFg in Enum.GetValues<C64Colors>())
        {
            var c64ColorValueFg = (byte)c64ColorFg; // Color 0-15
            var systemColorFg = GetSystemColor(c64ColorValueFg, colorMapName); // .NET "Color" type
            var skColorFg = c64SkiaColors.SystemToSkColorMap[systemColorFg];    // Skia "SKColor" type

            foreach (var c64ColorBg in Enum.GetValues<C64Colors>())
            {
                var c64ColorValueBg = (byte)c64ColorBg; // Color 0-15
                var systemColorBg = GetSystemColor(c64ColorValueBg, colorMapName); // .NET "Color" type
                var skColorBg = c64SkiaColors.SystemToSkColorMap[systemColorBg];    // Skia "SKColor" type

                var colorFilter = CreateReplaceColorFilter(skColorFg, CharGen.CharacterImageDrawColor, newBackgroundColor: skColorBg);
                var paint = new SKPaint { Style = SKPaintStyle.StrokeAndFill, ColorFilter = colorFilter };
                //var paint = new SKPaint { Style = SKPaintStyle.Stroke, ColorFilter = colorFilter };

                _c64ToDrawChargenCharacterMapWithBackground.Add((c64ColorValueFg, c64ColorValueBg), paint);
            }
        }

        // MultiColor text mode, pixel colors can be one of 3 different colors.
        // The color from color ram can only use the 8 first colors (0-7)
        foreach (var c64Color in Enum.GetValues<C64Colors>().Take(8))
        {
            var c64ColorValue = (byte)c64Color;                 // Color 0-7
            var systemColor = GetSystemColor(c64ColorValue, colorMapName);                  // .NET "Color" type
            var skColor = c64SkiaColors.SystemToSkColorMap[systemColor];                    // Skia "SKColor" type

            foreach (var c64MultiColorBG1 in Enum.GetValues<C64Colors>())
            {
                var c64MultiColorValueBG1 = (byte)c64MultiColorBG1; // Color 0-15
                var systemMultiColorBG1 = GetSystemColor(c64MultiColorValueBG1, colorMapName);  // .NET "Color" type
                var skMultiColorBG1 = c64SkiaColors.SystemToSkColorMap[systemMultiColorBG1];    // Skia "SKColor" type

                foreach (var c64MultiColorBG2 in Enum.GetValues<C64Colors>())
                {
                    var c64MultiColorValueBG2 = (byte)c64MultiColorBG2; // Color 0-15
                    var systemMultiColorBG2 = GetSystemColor(c64MultiColorValueBG2, colorMapName);  // .NET "Color" type
                    var skMultiColorBG2 = c64SkiaColors.SystemToSkColorMap[systemMultiColorBG2];    // Skia "SKColor" type

                    var colorFilter = CreateReplaceColorFilter(
                        skColor, CharGen.CharacterImageDrawColor,
                        skMultiColorBG1, CharGen.CharacterImageDrawMultiColorBG1,
                        skMultiColorBG2, CharGen.CharacterImageDrawMultiColorBG2);

                    var paint = new SKPaint { Style = SKPaintStyle.StrokeAndFill, ColorFilter = colorFilter };
                    //var paint = new SKPaint { Style = SKPaintStyle.Stroke, ColorFilter = colorFilter };

                    _c64ToDrawChargenCharacterMapWithMultiColor.Add((c64ColorValue, c64MultiColorValueBG1, c64MultiColorValueBG2), paint);
                }
            }
        }


        // Sprite colors
        foreach (var c64Color in Enum.GetValues<C64Colors>())
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

    public SKPaint GetDrawCharacterPaintWithMultiColor(byte c64Color, byte c64MultiColorBG1, byte c64MultiColorBG2)
    {
        c64Color &= 0x07; // Color range is only use first 3 bits (values 0-7)
        c64MultiColorBG1 &= 0x0f; // Color range is only use lower 4 bits (values 0-15)
        c64MultiColorBG2 &= 0x0f; // Color range is only use lower 4 bits (values 0-15)
        return _c64ToDrawChargenCharacterMapWithMultiColor[(c64Color, c64MultiColorBG1, c64MultiColorBG2)];
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
    private SKColorFilter CreateReplaceColorFilter(
        SKColor newColor,
        SKColor originalColor,
        SKColor? newColor2 = null,
        SKColor? originalColor2 = null,
        SKColor? newColor3 = null,
        SKColor? originalColor3 = null,
        SKColor? newBackgroundColor = null)
    {
        var R = new byte[256];
        var G = new byte[256];
        var B = new byte[256];
        var A = new byte[256];

        R[originalColor.Red] = newColor.Red;
        G[originalColor.Green] = newColor.Green;
        B[originalColor.Blue] = newColor.Blue;
        A[originalColor.Alpha] = newColor.Alpha;

        if (newColor2 != null && originalColor2 != null)
        {
            R[originalColor2!.Value.Red] = newColor2.Value.Red;
            G[originalColor2!.Value.Green] = newColor2.Value.Green;
            B[originalColor2!.Value.Blue] = newColor2.Value.Blue;
            A[originalColor2!.Value.Alpha] = newColor2.Value.Alpha;
        }
        if (newColor3 != null && originalColor3 != null)
        {
            R[originalColor3!.Value.Red] = newColor3.Value.Red;
            G[originalColor3!.Value.Green] = newColor3.Value.Green;
            B[originalColor3!.Value.Blue] = newColor3.Value.Blue;
            A[originalColor3!.Value.Alpha] = newColor3.Value.Alpha;
        }
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
