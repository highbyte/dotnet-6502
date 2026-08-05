using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Avalonia.Shell.Apple2.Apple2AvaloniaShellPlugin))]

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Apple2;

/// <summary>
/// Shell-side plugin for the Apple II on the Avalonia host.
/// </summary>
/// <remarks>
/// A stub for now: the Apple II contributes no system-specific menu, info panel or config
/// dialog yet, so the host renders only its system-agnostic chrome. The engine-side wiring
/// (the <c>ISystemConfigurer</c>) lives in the engine plugin <c>Apple2AvaloniaEnginePlugin</c>
/// (Impl.Avalonia.Apple2). This project's other job is to exist, so the entry exe's
/// <c>App.Avalonia.Shell.*</c> glob deploys the engine plugin with it.
/// </remarks>
public sealed class Apple2AvaloniaShellPlugin : ISystemShellPlugin
{
    public string SystemName => global::Highbyte.DotNet6502.Systems.Apple2.Apple2.SystemName;

    public int DisplayOrder => 30;

    public void RegisterShellServices(IServiceCollection services)
    {
        // No UI services yet — the Apple II has no Avalonia-specific shell UI.
    }

    public object? CreateMenuContribution(IServiceProvider sp) => null;

    public object? CreateInfoContribution(IServiceProvider sp) => null;

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
