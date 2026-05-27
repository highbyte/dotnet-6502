using Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;
using Highbyte.DotNet6502.Systems.Plugins;
using Highbyte.DotNet6502.Systems.Vic20;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.Vic20AvaloniaShellPlugin))]

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20;

/// <summary>
/// Shell-side plugin for the VIC-20 on the Avalonia host. Contributes a minimal but
/// non-null menu / info / config-dialog surface so the proof-of-contract exercise also
/// validates the three UI contribution paths (not just engine-side wiring). The contents
/// are intentionally tiny — the point is that the contribution objects are reachable
/// and that the host's ViewLocator can resolve their views.
/// </summary>
public sealed class Vic20AvaloniaShellPlugin : ISystemShellPlugin
{
    public string SystemName => global::Highbyte.DotNet6502.Systems.Vic20.Vic20.SystemName;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddTransient<Vic20MenuViewModel>();
        services.AddTransient<Vic20InfoViewModel>();
        services.AddTransient<Vic20ConfigDialogViewModel>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<Vic20MenuViewModel>();

    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<Vic20InfoViewModel>();

    public object? CreateConfigDialogContribution(IServiceProvider sp) => sp.GetService<Vic20ConfigDialogViewModel>();
}
