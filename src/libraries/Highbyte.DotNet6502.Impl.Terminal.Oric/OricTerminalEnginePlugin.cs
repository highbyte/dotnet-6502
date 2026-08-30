using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Terminal.Oric.OricTerminalEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Terminal.Oric;

/// <summary>Registers Oric Atmos emulation services for the Terminal host.</summary>
public sealed class OricTerminalEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => OricMachine.SystemName;

    public string HostTechName => "Terminal";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp =>
            new OricTerminalSetup(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>()));
    }
}
