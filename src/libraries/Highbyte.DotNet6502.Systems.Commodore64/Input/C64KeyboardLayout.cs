namespace Highbyte.DotNet6502.Systems.Commodore64.Input;

/// <summary>
/// The host keyboard physical layout the C64 keyboard mapping should assume.
///
/// Selects which layout-specific punctuation map <see cref="C64HostKeyboard"/> merges in. The
/// layout can be pinned via config (<see cref="C64InputConfig.KeyboardLayout"/>); when left unset
/// it is auto-detected from the host's native keyboard layout (<see cref="KeyboardLayoutResolver"/>),
/// falling back to the OS culture. Culture alone is unreliable —
/// <see cref="System.Globalization.CultureInfo"/> reports the UI/region language (e.g. "en"), not
/// the physical keyboard layout.
/// </summary>
public enum C64KeyboardLayout
{
    /// <summary>US (ANSI) physical keyboard layout.</summary>
    US,

    /// <summary>Swedish (ISO) physical keyboard layout.</summary>
    Swedish,
}
