using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Highbyte.DotNet6502.Systems.Oric.Input;

namespace Highbyte.DotNet6502.Impl.Avalonia.Oric;

public sealed class OricHostConfig : HostSystemConfigBase<OricSystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Oric.Avalonia";
    public override bool AudioSupported => true;

    public OricInputConfig InputConfig { get; set; } = new();

    public override object Clone()
    {
        var clone = (OricHostConfig)base.Clone();
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
