using Highbyte.DotNet6502.Systems.Generic.Video;

namespace Highbyte.DotNet6502.Impl.SadConsole.Generic.Video;

public static class GenericSadConsoleColors
{
    // Reuse 
    public static Dictionary<System.Drawing.Color, Color> SystemToSadConsoleColorMap = new();

    static GenericSadConsoleColors()
    {
        foreach (var systemColor in ColorMaps.GenericColorMap.Values)
        {
            SystemToSadConsoleColorMap.Add(systemColor, systemColor.ToSadConsoleColor());
        }
    }

    public static Color ToSadConsoleColor(this System.Drawing.Color color)
    {
        return new Color(color.R, color.G, color.B, color.A);
    }
}
