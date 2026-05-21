using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.Headless.Generic;

/// <summary>Generic-computer host config for the Headless host — no audio, no host-tech settings.</summary>
public class GenericComputerHeadlessHostConfig : HostSystemConfigBase<GenericComputerSystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer.Headless";

    public override bool AudioSupported => false;
}
