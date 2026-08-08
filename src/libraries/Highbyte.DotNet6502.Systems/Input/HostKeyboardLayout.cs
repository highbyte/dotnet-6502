namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// The host's physical keyboard layout, as far as an emulated system's keyboard mapping needs to
/// care about it.
///
/// System-neutral on purpose: which punctuation a host key produces is a property of the host
/// keyboard, not of the emulated machine, so several systems can share both this enum and
/// <see cref="HostKeyboardLayoutResolver"/> rather than each carrying a private copy of the same
/// platform-identifier switch.
///
/// Only layouts that at least one system has a specific punctuation map for belong here — an
/// unrecognised host layout resolves to <c>null</c> and the caller falls back to
/// <see cref="US"/>.
///
/// <para>
/// The C64 predates this and keeps its own <c>C64KeyboardLayout</c>. That is deliberate: its
/// value is persisted in user settings as a string ("Swedish"/"US"), so migrating it would risk
/// breaking saved configs for no user-visible gain.
/// </para>
/// </summary>
public enum HostKeyboardLayout
{
    /// <summary>US (ANSI) physical keyboard layout.</summary>
    US,

    /// <summary>Swedish (ISO) physical keyboard layout.</summary>
    Swedish,
}
