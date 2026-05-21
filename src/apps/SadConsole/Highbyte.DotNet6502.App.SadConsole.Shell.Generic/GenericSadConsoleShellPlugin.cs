using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.SadConsole.Shell.Generic.GenericSadConsoleShellPlugin))]

namespace Highbyte.DotNet6502.App.SadConsole.Shell.Generic;

/// <summary>
/// Shell-side plugin contributing the SadConsole Generic-computer info tab to the host shell.
/// </summary>
/// <remarks>
/// The Generic engine-side wiring (the <c>ISystemConfigurer</c>) lives in the engine plugin
/// <c>GenericSadConsoleEnginePlugin</c> (Impl.SadConsole.Generic) — this shell plugin is UI only.
/// </remarks>
public sealed class GenericSadConsoleShellPlugin : ISystemShellPlugin
{
    public string SystemName => GenericComputer.SystemName;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddSingleton<GenericSadConsoleInfoContribution>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => null;

    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<GenericSadConsoleInfoContribution>();

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
