using System.Drawing;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video;

public class C64SkiaColors
{
    /// <summary>
    /// .NET system Color to SkiaColor map for C64.
    /// </summary>
    /// <returns></returns>
    public Dictionary<Color, SKColor> SystemToSkColorMap = new();

    /// <summary>
    /// C64 to SkiaColor map for C64.
    /// </summary>
    /// <returns></returns>
    public Dictionary<byte, SKColor> C64ToSkColorMap = new();

    public C64SkiaColors(string colorMapName)
    {
        foreach (var systemColor in ColorMaps.GetAllSystemColors(colorMapName))
        {
            SystemToSkColorMap.Add(systemColor, ToSkColor(systemColor));
        }

        foreach (byte c64Color in Enum.GetValues<C64Colors>())
        {
            C64ToSkColorMap.Add(c64Color, ToSkColor(ColorMaps.GetSystemColor(c64Color, colorMapName)));
        }

    }

    private SKColor ToSkColor(Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }
}
