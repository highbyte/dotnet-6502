using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Oric.Config;

namespace Highbyte.DotNet6502.Impl.Avalonia.Oric;

public sealed class OricHostConfig : HostSystemConfigBase<OricSystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Oric.Avalonia";
    public override bool AudioSupported => true;
}
