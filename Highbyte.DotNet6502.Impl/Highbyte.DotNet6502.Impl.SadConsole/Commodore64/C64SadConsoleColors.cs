using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64;

public class C64SadConsoleColors
{
    private Dictionary<System.Drawing.Color, SadRogue.Primitives.Color> _systemToSadConsoleColorMap = new();

    public C64SadConsoleColors(string colorMapName)
    {
        foreach (var systemColor in ColorMaps.GetAllSystemColors(colorMapName))
        {
            _systemToSadConsoleColorMap.Add(systemColor, ToSadConsoleColor(systemColor));
        }
    }

    public SadRogue.Primitives.Color GetSadConsoleColor(System.Drawing.Color systemColorValue)
    {
        SadRogue.Primitives.Color color;
        if (!_systemToSadConsoleColorMap.ContainsKey(systemColorValue))
            color = _systemToSadConsoleColorMap.Values.First();
        else
            color = _systemToSadConsoleColorMap[systemColorValue];
        return color;
    }

    private SadRogue.Primitives.Color ToSadConsoleColor(System.Drawing.Color color)
    {
        return new Color(color.R, color.G, color.B, color.A);
    }
}
