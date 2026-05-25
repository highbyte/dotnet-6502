using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Vic20.Config;

namespace Highbyte.DotNet6502.Impl.Avalonia.Vic20;

/// <summary>VIC-20 host config for the Avalonia host.</summary>
public class Vic20HostConfig : HostSystemConfigBase<Vic20SystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Vic20.Avalonia";

    public override bool AudioSupported => false;

    public override object Clone() => (Vic20HostConfig)base.Clone();
}
