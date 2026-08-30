using Highbyte.DotNet6502.Systems.Oric.Input;

namespace Highbyte.DotNet6502.Systems.Oric;

/// <summary>Fixed timing and display geometry for a PAL Oric Atmos.</summary>
public sealed class OricConfig
{
    public const int MasterClockHz = 12_000_000;
    public const int CpuFrequencyHz = MasterClockHz / 12;
    public const int AyFrequencyHz = MasterClockHz / 12;
    public const int CyclesPerLine = 64;
    public const int LinesPerFrame = 312;
    public const ulong CpuCyclesPerFrame = CyclesPerLine * LinesPerFrame;
    public const float ScreenRefreshFrequencyHz = (float)CpuFrequencyHz / (CyclesPerLine * LinesPerFrame);

    public const int Columns = 40;
    public const int CharacterWidth = 6;
    public const int CharacterHeight = 8;
    public const int VisibleWidth = Columns * CharacterWidth;
    public const int VisibleHeight = 224;
    public const int HiResHeight = 200;

    public CpuCompatibilityProfile CpuCompatibilityProfile { get; set; } = CpuCompatibilityProfile.StableUnofficial;
    public bool AudioEnabled { get; set; } = true;
    public Type? AudioProviderType { get; set; }
    public OricJoystickInterface JoystickInterface { get; set; } = OricJoystickInterface.None;
    public bool KeyboardJoystickEnabled { get; set; }
    public int KeyboardJoystick { get; set; } = 1;
}
