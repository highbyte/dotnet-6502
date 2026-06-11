using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Terminal.Vic20.Vic20TerminalEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Terminal.Vic20;

/// <summary>
/// Engine-side plugin for the VIC-20 on the Terminal (TUI) host. Registers the VIC-20
/// <see cref="ISystemConfigurer"/> (<see cref="Vic20TerminalSetup"/>) into the host's DI container,
/// so the terminal host can run the VIC-20 without holding any compile-time reference to it.
///
/// The VIC-20 ships no shell plugin (no system-specific menu) — the host shows only the standard
/// controls for it, demonstrating a system without a menu contribution.
/// </summary>
public sealed class Vic20TerminalEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => Vic20System.SystemName;

    public string HostTechName => "Terminal";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp =>
            new Vic20TerminalSetup(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>()));
    }
}
