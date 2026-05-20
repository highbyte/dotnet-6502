using Highbyte.DotNet6502.App.WASM.Emulator;
using Highbyte.DotNet6502.App.WASM.Shell.Commodore64.Pages.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.WASM.Shell.Commodore64.C64WasmShellPlugin))]

namespace Highbyte.DotNet6502.App.WASM.Shell.Commodore64;

/// <summary>
/// Shell-side plugin contributing the C64 Blazor menu / help / config components to the WASM host.
/// </summary>
/// <remarks>
/// The C64 engine-side wiring (the <c>ISystemConfigurer</c>) lives in the engine plugin
/// <c>C64AspNetEnginePlugin</c> (Impl.AspNet.Commodore64) — this shell plugin is UI only.
/// </remarks>
public sealed class C64WasmShellPlugin : ISystemShellPlugin
{
    public string SystemName => C64.SystemName;

    public void RegisterShellServices(IServiceCollection services)
    {
        // No engine services — the C64 ISystemConfigurer is registered by C64AspNetEnginePlugin.
    }

    public object? CreateMenuContribution(IServiceProvider serviceProvider) => new WasmMenuContribution(typeof(C64Menu));

    public object? CreateInfoContribution(IServiceProvider serviceProvider) => new WasmHelpContribution(typeof(C64MenuHelp));

    public object? CreateConfigDialogContribution(IServiceProvider serviceProvider) => new WasmConfigDialogContribution(
        typeof(C64ConfigUI),
        UseRenderProviderAndRenderTargetTypeCombinations: true);
}
