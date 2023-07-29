using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64;

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

    public C64SkiaPaint(string colorMapName)
    {
        var c64SkiaColors = new C64SkiaColors(colorMapName);

        foreach (var c64Color in Enum.GetValues(typeof(C64Colors)))
        {
            var c64ColorValue = (byte)c64Color; // Color 0-15
            var systemColor = ColorMaps.GetSystemColor(c64ColorValue, colorMapName); // .NET "Color" type
            var skColor = c64SkiaColors.SystemToSkColorMap[systemColor];    // Skia "SKColor" type
            var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = skColor };

            _c64ToFillPaintMap.Add(c64ColorValue, paint);
        }

        foreach (var c64Color in Enum.GetValues(typeof(C64Colors)))
        {
            var c64ColorValue = (byte)c64Color; // Color 0-15
            var systemColor = ColorMaps.GetSystemColor(c64ColorValue, colorMapName); // .NET "Color" type
            var skColor = c64SkiaColors.SystemToSkColorMap[systemColor];    // Skia "SKColor" type
            var colorFilter = CreateReplaceColorFilter(skColor, Chargen.CharacterImageDrawColor);
            var paint = new SKPaint { Style = SKPaintStyle.StrokeAndFill, ColorFilter = colorFilter };
            //var paint = new SKPaint { Style = SKPaintStyle.Stroke, ColorFilter = colorFilter };

            _c64ToDrawChargenCharacterMap.Add(c64ColorValue, paint);
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

    /// <summary>
    /// Color filter change the color the original character image was drawn in to a specified color.
    /// </summary>
    /// <param name="newColor"></param>
    /// <param name="originalColor"></param>
    /// <returns></returns>
    private SKColorFilter CreateReplaceColorFilter(SKColor newColor, SKColor originalColor)
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
