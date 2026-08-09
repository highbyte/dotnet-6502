using Highbyte.DotNet6502.Systems.Apple2.Video;

namespace Highbyte.DotNet6502.Systems.Apple2.Config;

/// <summary>
/// Machine constants and non-user-facing settings for the Apple II.
/// The user-facing counterpart is <see cref="Apple2SystemConfig"/>.
/// </summary>
public class Apple2Config
{
    public const int Cols = Apple2TextScreen.Columns;
    public const int Rows = Apple2TextScreen.Rows;

    /// <summary>
    /// The hardware character cell: 7x8 pixels. The character generator stores 5 dot columns
    /// per scan line and the remaining 2 columns are the inter-character gap.
    /// </summary>
    public const int CharacterWidth = 7;
    public const int CharacterHeight = 8;

    public const int DrawableAreaWidth = Cols * CharacterWidth;    // 280
    public const int DrawableAreaHeight = Rows * CharacterHeight;  // 192

    /// <summary>
    /// In mixed mode ($C053) the bottom 4 text rows stay text while graphics fill the area above
    /// them (160 scan lines).
    /// </summary>
    public const int MixedModeTextRows = 4;
    public const int MixedModeFirstTextRow = Rows - MixedModeTextRows;                  // 20
    public const int MixedModeGraphicsHeight = MixedModeFirstTextRow * CharacterHeight; // 160

    /// <summary>
    /// The Apple II video signal has no separately coloured border area like the VIC-I or
    /// VIC-II — the text field fills the active display.
    /// </summary>
    public const bool HasBorder = false;

    /// <summary>Frames between flash-attribute toggles: roughly the hardware's ~2 Hz blink.</summary>
    public const int FlashFramesPerToggle = 15;

    /// <summary>
    /// 65 CPU cycles per scan line x 262 lines. At the Apple II's 1.0205 MHz that is ~59.92 Hz.
    /// </summary>
    public ulong CpuCyclesPerFrame { get; set; } = 17030;

    public float ScreenRefreshFrequencyHz { get; set; } = 59.92f;

    public Apple2MonitorColor MonitorColor { get; set; } = Apple2MonitorColor.Color;

    /// <summary>
    /// Whether to build an audio provider at all. With this off no provider is created, so the
    /// host builds no audio coordinator and the machine stays silent — matching how the C64 does it.
    /// </summary>
    public bool AudioEnabled { get; set; }

    /// <summary>
    /// Whether the 16 KB language card is fitted. On real hardware it was an expansion card rather
    /// than part of a stock II Plus, so switching it off gives a genuine 48 KB machine.
    /// </summary>
    public bool LanguageCardEnabled { get; set; } = true;

    /// <summary>Which audio provider to select; defaults to the only one when unset.</summary>
    public Type? AudioProviderType { get; set; }

    public CpuCompatibilityProfile CpuCompatibilityProfile { get; set; } = CpuCompatibilityProfile.StableUnofficial;
}
