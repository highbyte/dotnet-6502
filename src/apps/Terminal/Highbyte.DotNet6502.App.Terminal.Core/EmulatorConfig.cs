using Highbyte.DotNet6502.Monitor;

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>Top-level Terminal host configuration (bound from appsettings.json).</summary>
public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.TerminalConfig";

    /// <summary>The system selected at startup. Must be one of the enabled systems (e.g. "C64").</summary>
    public string DefaultEmulator { get; set; } = "C64";

    /// <summary>
    /// How often the terminal screen is repainted, in Hz. Kept below the emulator frame rate to
    /// stay smooth on slower terminals; the emulator itself still runs at its native frame rate.
    /// </summary>
    public int DisplayRefreshHz { get; set; } = 30;

    /// <summary>
    /// How many cosmetic border cells to crop from the top and bottom of the emulated screen when
    /// painting it in the terminal. The emulated systems render their own (solid-colour) screen
    /// border, which for the C64/VIC-20 is 2–3 cells tall — taller than needed in a terminal where
    /// every row is precious. Cropping 1 cell on each side lets a C64 (29 cells tall incl. border)
    /// fit a default 30-row terminal without the user resizing the window, while still showing a
    /// border. 0 disables cropping (shows the full emulated border). Only affects the terminal view;
    /// the shared render command stream (and other hosts) are unaffected.
    /// </summary>
    public int ScreenBorderTrim { get; set; } = 1;

    /// <summary>Machine-code monitor options (shared with the other host apps' monitor).</summary>
    public MonitorConfig Monitor { get; set; } = new();
}
