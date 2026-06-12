using Highbyte.DotNet6502.Monitor;

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>Top-level Terminal host configuration (bound from appsettings.json).</summary>
public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.TerminalConfig";

    /// <summary>The system selected at startup. Must be one of the enabled systems (e.g. "C64").</summary>
    public string DefaultEmulator { get; set; } = "C64";

    /// <summary>
    /// The host-command "leader" key. Pressing it arms host mode; the next keystroke selects a command
    /// (Start/Stop, Monitor, Stats, Quit, cycle System/Variant). A single leader key keeps every other
    /// key free for the emulated system, and avoids keys terminals reserve (F10 = menu, F11 =
    /// fullscreen). Accepts a <c>KeyCode</c> name such as "F9" (default) or "F12"; an unrecognised
    /// value falls back to F9.
    /// </summary>
    public string LeaderKey { get; set; } = "F9";

    /// <summary>
    /// How often the terminal screen is repainted, in Hz. Kept below the emulator frame rate to
    /// stay smooth on slower terminals; the emulator itself still runs at its native frame rate.
    /// </summary>
    public int DisplayRefreshHz { get; set; } = 30;

    // The emulated systems render their own thick solid-colour screen border, which wastes space in a
    // terminal where every row/column is precious. These two settings cap how much of that border the
    // terminal view keeps on each side; it crops any excess so every system shows a consistent thin
    // border (a system whose border is already this thin, or thinner, is left untouched). They only
    // affect the terminal view — the shared render command stream (and other hosts) are unaffected.

    /// <summary>
    /// Border rows to keep on the top and bottom of the emulated screen. The C64/VIC-20 border is
    /// ~2 rows tall; keeping 1 lets a C64 (29 rows incl. border) fit a default 30-row terminal without
    /// resizing, while still showing a border. 0 disables vertical cropping (full border).
    /// </summary>
    public int VerticalBorderRows { get; set; } = 1;

    /// <summary>
    /// Border columns to keep on the left and right of the emulated screen. The side border is wide
    /// (the C64 ~6 columns, the VIC-20 ~5); keeping 2 trims that to a thin, consistent border.
    /// 0 disables horizontal cropping (full border).
    /// </summary>
    public int HorizontalBorderColumns { get; set; } = 2;

    /// <summary>Machine-code monitor options (shared with the other host apps' monitor).</summary>
    public MonitorConfig Monitor { get; set; } = new();
}
