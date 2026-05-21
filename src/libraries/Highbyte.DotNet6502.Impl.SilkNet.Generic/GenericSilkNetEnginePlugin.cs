using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.SilkNet.Generic.GenericSilkNetEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.SilkNet.Generic;

/// <summary>
/// Engine-side plugin for the Generic computer on the SilkNet + NAudio host pair. Registers the
/// Generic <see cref="ISystemConfigurer{TIn,TAu}"/> into DI. Contributes no render targets — the
/// Generic computer uses only the host's system-agnostic targets.
/// </summary>
public sealed class GenericSilkNetEnginePlugin
    : ISystemEnginePlugin
{
    public string SystemName => GenericComputer.SystemName;

    public string HostTechName => "SilkNet.NAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Engine-side configurer for the SilkNet + NAudio host pair.
        services.AddSingleton<ISystemConfigurer>(sp =>
            new GenericComputerSetup(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>()));
    }
}
