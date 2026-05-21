using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Headless.Commodore64.C64HeadlessEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Headless.Commodore64;

/// <summary>
/// Engine-side plugin for the C64 on the Headless host. Registers the shared
/// <see cref="C64SystemConfigurerCore"/> directly into DI — the Headless host has no
/// host-technology glue, so the core configurer is the complete C64 implementation for it.
/// See <c>docs/system-configurer-consolidation.md</c>.
/// </summary>
public sealed class C64HeadlessEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => C64.SystemName;

    public string HostTechName => "Headless";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp =>
            new C64SystemConfigurerCore(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>(),
                () => new C64HeadlessHostConfig(),
                C64HeadlessHostConfig.ConfigSectionName));
    }
}
