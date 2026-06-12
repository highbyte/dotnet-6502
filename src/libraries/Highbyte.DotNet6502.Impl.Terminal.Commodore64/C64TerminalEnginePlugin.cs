using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Terminal.Commodore64.C64TerminalEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Terminal.Commodore64;

/// <summary>
/// Engine-side plugin for the C64 on the Terminal (TUI) host. Registers the C64
/// <see cref="ISystemConfigurer"/> (<see cref="C64TerminalSetup"/>) into the host's DI container, so
/// the terminal host can run the C64 without holding any compile-time reference to it.
/// </summary>
public sealed class C64TerminalEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => C64.SystemName;

    public string HostTechName => "Terminal";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp =>
            new C64TerminalSetup(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>()));
    }
}
