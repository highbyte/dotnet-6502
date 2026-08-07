using System.Drawing;

namespace Highbyte.DotNet6502.Systems.Apple2.Video;

/// <summary>
/// The kind of display the Apple II is plugged into. The machine emits one composite signal;
/// what you see depends on the monitor. On a monochrome monitor the hi-res bit stream is just
/// a dot pattern in the phosphor's color, while a color monitor decodes the same dots as
/// NTSC artifact colors (see <see cref="Apple2HiResColors"/>).
///
/// Serialized numerically, so new members must be appended to keep saved settings stable.
/// </summary>
public enum Apple2MonitorColor
{
    Green,
    White,
    Amber,
    Color,
}

/// <summary>Foreground/background colors for the Apple II text display.</summary>
public static class Apple2Colors
{
    public static readonly Color Background = Color.FromArgb(255, 0, 0, 0);

    /// <summary>Whether the monitor decodes hi-res dots as NTSC artifact colors.</summary>
    public static bool IsColorMonitor(Apple2MonitorColor monitorColor)
        => monitorColor == Apple2MonitorColor.Color;

    public static Color GetForeground(Apple2MonitorColor monitorColor) => monitorColor switch
    {
        Apple2MonitorColor.White => Color.FromArgb(255, 255, 255, 255),
        Apple2MonitorColor.Amber => Color.FromArgb(255, 255, 176, 0),
        // Text has no artifact color of its own — a color monitor renders it white.
        Apple2MonitorColor.Color => Color.FromArgb(255, 255, 255, 255),
        _ => Color.FromArgb(255, 51, 255, 51),
    };
}
