using System.Drawing;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64;

public class C64SkiaColors
{
    /// <summary>
    /// .NET system Color to SkiaColor map for C64.
    /// </summary>
    /// <returns></returns>
    public Dictionary<System.Drawing.Color, SKColor> SystemToSkColorMap = new();

    public C64SkiaColors(string colorMapName)
    {
        foreach (var systemColor in ColorMaps.GetAllSystemColors(colorMapName))
        {
            SystemToSkColorMap.Add(systemColor, ToSkColor(systemColor));
        }
    }

    private SKColor ToSkColor(Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }
}
