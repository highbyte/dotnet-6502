using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Headless.Generic.GenericHeadlessEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Headless.Generic;

/// <summary>
/// Engine-side plugin for the Generic computer on the Headless host. Registers
/// <see cref="GenericComputerHeadlessSetup"/> — a thin <see cref="GenericComputerSystemConfigurerCore"/>
/// subclass that loads example programs from this assembly's embedded <c>.prg</c> resources.
/// See <c>docs/system-configurer-consolidation.md</c>.
/// </summary>
public sealed class GenericHeadlessEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => GenericComputer.SystemName;

    public string HostTechName => "Headless";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp =>
            new GenericComputerHeadlessSetup(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>()));
    }
}
