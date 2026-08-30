using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Terminal.Shell.Oric.OricTerminalShellPlugin))]

namespace Highbyte.DotNet6502.App.Terminal.Shell.Oric;

/// <summary>Contributes Oric Atmos controls and keyboard information to the Terminal host.</summary>
public sealed class OricTerminalShellPlugin : ISystemShellPlugin
{
    public string SystemName => OricMachine.SystemName;

    public int DisplayOrder => 40;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddSingleton<OricTerminalMenuView>(sp => new OricTerminalMenuView(
            sp.GetRequiredService<TuiHostApp>(),
            sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<OricTerminalInfoView>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<OricTerminalMenuView>();

    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<OricTerminalInfoView>();

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
