using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.SilkNet.Generic;

/// <summary>Generic-computer host config for the SilkNet host.</summary>
public class GenericComputerHostConfig : HostSystemConfigBase<GenericComputerSystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer.SilkNetNative";

    public override bool AudioSupported => false;
}
