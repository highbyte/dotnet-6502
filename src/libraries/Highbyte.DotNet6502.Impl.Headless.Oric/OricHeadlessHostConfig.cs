using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Oric.Config;

namespace Highbyte.DotNet6502.Impl.Headless.Oric;

/// <summary>Oric host configuration for the Headless host, which has no audio target.</summary>
public sealed class OricHeadlessHostConfig : HostSystemConfigBase<OricSystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Oric.Headless";

    public override bool AudioSupported => false;
}
