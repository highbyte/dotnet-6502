using Highbyte.DotNet6502.App.WASM.Emulator;
using Highbyte.DotNet6502.App.WASM.Shell.Generic.Pages.Generic;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.WASM.Shell.Generic.GenericWasmShellPlugin))]

namespace Highbyte.DotNet6502.App.WASM.Shell.Generic;

/// <summary>
/// Shell-side plugin contributing the Generic-computer Blazor menu / help / config components to
/// the WASM host.
/// </summary>
/// <remarks>
/// The Generic engine-side wiring (the <c>ISystemConfigurer</c>) lives in the engine plugin
/// <c>GenericAspNetEnginePlugin</c> (Impl.AspNet.Generic) — this shell plugin is UI only.
/// </remarks>
public sealed class GenericWasmShellPlugin : ISystemShellPlugin
{
    public string SystemName => GenericComputer.SystemName;

    public void RegisterShellServices(IServiceCollection services)
    {
        // No engine services — the Generic ISystemConfigurer is registered by GenericAspNetEnginePlugin.
    }

    public object? CreateMenuContribution(IServiceProvider serviceProvider) => new WasmMenuContribution(typeof(GenericMenu));

    public object? CreateInfoContribution(IServiceProvider serviceProvider) => new WasmHelpContribution(typeof(GenericMenuHelp));

    public object? CreateConfigDialogContribution(IServiceProvider serviceProvider) => new WasmConfigDialogContribution(typeof(GenericConfigUI));
}
