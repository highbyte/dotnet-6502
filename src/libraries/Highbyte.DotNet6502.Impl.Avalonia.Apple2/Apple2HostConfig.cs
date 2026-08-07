using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Input;

namespace Highbyte.DotNet6502.Impl.Avalonia.Apple2;

/// <summary>Apple II host config for the Avalonia host.</summary>
public class Apple2HostConfig : HostSystemConfigBase<Apple2SystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Apple2.Avalonia";

    /// <summary>
    /// The speaker is emulated as PCM via <c>Apple2SpeakerSampleProvider</c>.
    /// </summary>
    public override bool AudioSupported => true;

    /// <summary>Gamepad and keyboard-joystick mapping for the game port.</summary>
    public Apple2InputConfig InputConfig { get; set; } = new();

    public override object Clone()
    {
        var clone = (Apple2HostConfig)base.Clone();
        clone.InputConfig = (Apple2InputConfig)InputConfig.Clone();
        return clone;
    }
}
