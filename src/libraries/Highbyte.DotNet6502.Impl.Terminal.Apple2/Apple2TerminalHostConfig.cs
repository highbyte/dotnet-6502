using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Systems.Apple2.Render;

namespace Highbyte.DotNet6502.Impl.Terminal.Apple2;

/// <summary>Apple II host config for the Terminal host: text commands, keyboard input, no audio.</summary>
public class Apple2TerminalHostConfig : HostSystemConfigBase<Apple2SystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Apple2.Terminal";

    public override bool AudioSupported => false;

    public Apple2InputConfig InputConfig { get; set; } = new();

    public Apple2TerminalHostConfig()
    {
        SystemConfig.AudioEnabled = false;
        SystemConfig.SetRenderProviderType(typeof(Apple2VideoCommandStream));
    }

    public override object Clone()
    {
        var clone = (Apple2TerminalHostConfig)base.Clone();
        clone.InputConfig = (Apple2InputConfig)InputConfig.Clone();
        return clone;
    }
}
