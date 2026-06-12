using Highbyte.DotNet6502.App.Terminal;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Terminal.Shell.Vic20.Vic20TerminalShellPlugin))]

namespace Highbyte.DotNet6502.App.Terminal.Shell.Vic20;

/// <summary>
/// Shell-side plugin contributing the VIC-20's terminal menu control and info panel to the host.
/// </summary>
/// <remarks>
/// The VIC-20 engine-side wiring (the <c>ISystemConfigurer</c>) lives in the engine plugin
/// <c>Vic20TerminalEnginePlugin</c> (Impl.Terminal.Vic20) — this shell plugin is UI only.
/// </remarks>
public sealed class Vic20TerminalShellPlugin : ISystemShellPlugin
{
    public string SystemName => Vic20System.SystemName;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddSingleton<Vic20TerminalMenuView>(sp => new Vic20TerminalMenuView(
            sp.GetRequiredService<TuiHostApp>(),
            sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<Vic20TerminalInfoView>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<Vic20TerminalMenuView>();

    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<Vic20TerminalInfoView>();

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
