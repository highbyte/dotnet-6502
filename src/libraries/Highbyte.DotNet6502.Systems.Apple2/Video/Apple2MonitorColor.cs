using System.Drawing;

namespace Highbyte.DotNet6502.Systems.Apple2.Video;

/// <summary>
/// Phosphor colour of the emulated monitor. The Apple II text display is monochrome — the
/// colour is a property of the screen it was plugged into, not of the machine.
/// </summary>
public enum Apple2MonitorColor
{
    Green,
    White,
    Amber,
}

/// <summary>Foreground/background colours for the monochrome Apple II text display.</summary>
public static class Apple2Colors
{
    public static readonly Color Background = Color.FromArgb(255, 0, 0, 0);

    public static Color GetForeground(Apple2MonitorColor monitorColor) => monitorColor switch
    {
        Apple2MonitorColor.White => Color.FromArgb(255, 255, 255, 255),
        Apple2MonitorColor.Amber => Color.FromArgb(255, 255, 176, 0),
        _ => Color.FromArgb(255, 51, 255, 51),
    };
}
