using Highbyte.DotNet6502.App.SadConsole.Core;
using Highbyte.DotNet6502.App.SadConsole.Shell.Commodore64.ConfigUI;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.SadConsole.Shell.Commodore64.C64SadConsoleShellPlugin))]

namespace Highbyte.DotNet6502.App.SadConsole.Shell.Commodore64;

/// <summary>
/// Shell-side plugin contributing the SadConsole C64 menu console and info tab to the host shell.
/// </summary>
/// <remarks>
/// The C64 engine-side wiring (the <c>ISystemConfigurer</c>) lives in the engine plugin
/// <c>C64SadConsoleEnginePlugin</c> (Impl.SadConsole.Commodore64) — this shell plugin is UI only.
/// </remarks>
public sealed class C64SadConsoleShellPlugin : ISystemShellPlugin
{
    public string SystemName => C64.SystemName;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddSingleton<C64MenuConsole>(sp => new C64MenuConsole(
            sp.GetRequiredService<SadConsoleHostApp>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<IConfiguration>()));
        services.AddSingleton<C64SadConsoleInfoContribution>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<C64MenuConsole>();

    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<C64SadConsoleInfoContribution>();

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
