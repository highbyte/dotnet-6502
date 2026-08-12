using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Terminal.Apple2.Apple2TerminalEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Terminal.Apple2;

/// <summary>Registers Apple II emulation services for the Terminal host.</summary>
public sealed class Apple2TerminalEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => global::Highbyte.DotNet6502.Systems.Apple2.Apple2.SystemName;

    public string HostTechName => "Terminal";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp =>
            new Apple2TerminalSetup(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>()));
    }
}
