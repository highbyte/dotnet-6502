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
}
