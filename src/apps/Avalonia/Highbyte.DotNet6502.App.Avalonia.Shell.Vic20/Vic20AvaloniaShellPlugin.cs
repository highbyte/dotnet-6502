using Highbyte.DotNet6502.Systems.Plugins;
using Highbyte.DotNet6502.Systems.Vic20;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.Vic20AvaloniaShellPlugin))]

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20;

/// <summary>
/// Shell-side plugin for the VIC-20 on the Avalonia host.
/// All contributions are null — the VIC-20 proof-of-contract exercise relies on the host's
/// system-agnostic chrome only (no custom menu, info panel, or config dialog).
/// </summary>
public sealed class Vic20AvaloniaShellPlugin : ISystemShellPlugin
{
    public string SystemName => global::Highbyte.DotNet6502.Systems.Vic20.Vic20.SystemName;

    public void RegisterShellServices(IServiceCollection services) { }

    public object? CreateMenuContribution(IServiceProvider sp) => null;

    public object? CreateInfoContribution(IServiceProvider sp) => null;

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
