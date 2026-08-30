using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Headless.Oric.OricHeadlessEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Headless.Oric;

/// <summary>
/// Engine-side plugin for the Oric Atmos on the Headless host. The shared
/// <see cref="OricSystemConfigurerCore"/> supplies the complete machine implementation because
/// Headless has no host keyboard, display target, or audio target to wire.
/// </summary>
public sealed class OricHeadlessEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => OricMachine.SystemName;

    public string HostTechName => "Headless";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp =>
            new OricSystemConfigurerCore(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>(),
                () => new OricHeadlessHostConfig(),
                OricHeadlessHostConfig.ConfigSectionName));
    }
}
