using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Vic20.Config;

namespace Highbyte.DotNet6502.Impl.Avalonia.Vic20;

/// <summary>VIC-20 host config for the Avalonia host.</summary>
public class Vic20HostConfig : HostSystemConfigBase<Vic20SystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Vic20.Avalonia";

    public override bool AudioSupported => false;

    // The CORS proxy is now a general browser setting (EmulatorConfig.CorsProxyUrl), no longer
    // per-system. See AvaloniaHostApp.GetCorsProxyUrl().

    public override object Clone() => (Vic20HostConfig)base.Clone();
}
