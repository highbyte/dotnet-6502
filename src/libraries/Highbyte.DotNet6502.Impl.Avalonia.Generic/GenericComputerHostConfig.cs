using Highbyte.DotNet6502.Impl.Avalonia.Generic.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.Avalonia.Generic;

/// <summary>Generic-computer host config for the Avalonia host.</summary>
public class GenericComputerHostConfig : HostSystemConfigBase<GenericComputerSystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer.Avalonia";

    public override bool AudioSupported => false; // Generic computer doesn't have audio

    public GenericComputerAvaloniaInputConfig InputConfig { get; set; } = new GenericComputerAvaloniaInputConfig();

    public override object Clone()
    {
        var clone = (GenericComputerHostConfig)base.Clone();
        clone.InputConfig = (GenericComputerAvaloniaInputConfig)InputConfig.Clone();
        return clone;
    }
}
