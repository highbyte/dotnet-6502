using System.Drawing;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64;

public static class C64SkiaColors
{
    /// <summary>
    /// .NET system Color to SkiaColor map for C64.
    /// </summary>
    /// <returns></returns>
    public static Dictionary<System.Drawing.Color, SKColor> SystemToSkColorMap = new();

    static C64SkiaColors()
    {
        foreach (var systemColor in ColorMaps.GetAllSystemColors())
        {
            SystemToSkColorMap.Add(systemColor, systemColor.ToSkColor());
        }
    }

    private static SKColor ToSkColor(this Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }
}
