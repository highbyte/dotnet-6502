using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Systems.Oric.Render;

namespace Highbyte.DotNet6502.Impl.Terminal.Oric;

/// <summary>Oric configuration for the Terminal host: text commands, keyboard input, no audio.</summary>
public sealed class OricTerminalHostConfig : HostSystemConfigBase<OricSystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Oric.Terminal";

    public override bool AudioSupported => false;

    public OricInputConfig InputConfig { get; set; } = new();

    public OricTerminalHostConfig()
    {
        SystemConfig.AudioEnabled = false;
        SystemConfig.SetRenderProviderType(typeof(OricVideoCommandStream));
    }

    public override object Clone()
    {
        var clone = (OricTerminalHostConfig)base.Clone();
        clone.InputConfig = (OricInputConfig)InputConfig.Clone();
        return clone;
    }

    public override bool IsValid(out List<string> validationErrors)
    {
        var isValid = base.IsValid(out validationErrors);
        if (InputConfig.CurrentJoystick is not (1 or 2))
            validationErrors.Add($"{nameof(InputConfig)}.{nameof(InputConfig.CurrentJoystick)} must be 1 or 2.");
        return isValid && validationErrors.Count == 0;
    }
}
