using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Video;

public class C64SadConsoleColors
{
    private Dictionary<System.Drawing.Color, Color> _systemToSadConsoleColorMap = new();

    public C64SadConsoleColors(string colorMapName)
    {
        foreach (var systemColor in ColorMaps.GetAllSystemColors(colorMapName))
        {
            _systemToSadConsoleColorMap.Add(systemColor, ToSadConsoleColor(systemColor));
        }
    }

    public Color GetSadConsoleColor(System.Drawing.Color systemColorValue)
    {
        Color color;
        if (!_systemToSadConsoleColorMap.ContainsKey(systemColorValue))
            color = _systemToSadConsoleColorMap.Values.First();
        else
            color = _systemToSadConsoleColorMap[systemColorValue];
        return color;
    }

    private Color ToSadConsoleColor(System.Drawing.Color color)
    {
        return new Color(color.R, color.G, color.B, color.A);
    }
}
