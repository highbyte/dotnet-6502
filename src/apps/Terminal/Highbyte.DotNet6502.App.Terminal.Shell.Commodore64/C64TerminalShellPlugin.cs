using Highbyte.DotNet6502.App.Terminal;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Terminal.Shell.Commodore64.C64TerminalShellPlugin))]

namespace Highbyte.DotNet6502.App.Terminal.Shell.Commodore64;

/// <summary>
/// Shell-side plugin contributing the C64's terminal menu control to the host's controls column.
/// </summary>
/// <remarks>
/// The C64 engine-side wiring (the <c>ISystemConfigurer</c>) lives in the engine plugin
/// <c>C64TerminalEnginePlugin</c> (Impl.Terminal.Commodore64) — this shell plugin is UI only.
/// </remarks>
public sealed class C64TerminalShellPlugin : ISystemShellPlugin
{
    public string SystemName => C64.SystemName;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddSingleton<C64TerminalMenuView>(sp => new C64TerminalMenuView(
            sp.GetRequiredService<TuiHostApp>(),
            sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<C64TerminalInfoView>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<C64TerminalMenuView>();

    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<C64TerminalInfoView>();

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
