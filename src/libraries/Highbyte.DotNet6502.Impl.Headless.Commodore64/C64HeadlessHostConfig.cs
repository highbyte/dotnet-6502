using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.Impl.Headless.Commodore64;

/// <summary>C64 host config for the Headless host — no audio, no host-tech settings.</summary>
public class C64HeadlessHostConfig : HostSystemConfigBase<C64SystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.Headless";

    public override bool AudioSupported => false;
}
