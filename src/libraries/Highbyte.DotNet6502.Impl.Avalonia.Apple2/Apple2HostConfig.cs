using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Apple2.Config;

namespace Highbyte.DotNet6502.Impl.Avalonia.Apple2;

/// <summary>Apple II host config for the Avalonia host.</summary>
public class Apple2HostConfig : HostSystemConfigBase<Apple2SystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Apple2.Avalonia";

    public override bool AudioSupported => false;

    public override object Clone() => (Apple2HostConfig)base.Clone();
}
