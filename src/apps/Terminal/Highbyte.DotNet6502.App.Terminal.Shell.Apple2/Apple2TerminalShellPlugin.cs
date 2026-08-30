using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Terminal.Shell.Apple2.Apple2TerminalShellPlugin))]

namespace Highbyte.DotNet6502.App.Terminal.Shell.Apple2;

/// <summary>Contributes Apple II controls and keyboard information to the Terminal host.</summary>
public sealed class Apple2TerminalShellPlugin : ISystemShellPlugin
{
    public string SystemName => Apple2System.SystemName;

    public int DisplayOrder => 30;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddSingleton<Apple2TerminalMenuView>(sp => new Apple2TerminalMenuView(
            sp.GetRequiredService<TuiHostApp>(),
            sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<Apple2TerminalInfoView>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<Apple2TerminalMenuView>();

    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<Apple2TerminalInfoView>();

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
