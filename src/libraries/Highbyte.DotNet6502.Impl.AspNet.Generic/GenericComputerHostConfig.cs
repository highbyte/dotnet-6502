using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.AspNet.Generic;

/// <summary>Generic-computer host config for the WASM (Blazor) host.</summary>
public class GenericComputerHostConfig : HostSystemConfigBase<GenericComputerSystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer.WASM";

    public override bool AudioSupported => false;
}
