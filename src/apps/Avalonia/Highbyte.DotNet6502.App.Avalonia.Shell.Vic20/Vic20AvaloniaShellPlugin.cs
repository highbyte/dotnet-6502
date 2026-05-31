using Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.Systems.Plugins;
using Highbyte.DotNet6502.Systems.Vic20;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.Vic20AvaloniaShellPlugin))]

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20;

/// <summary>
/// Shell-side plugin for the VIC-20 on the Avalonia host.
/// </summary>
public sealed class Vic20AvaloniaShellPlugin : ISystemShellPlugin, IAvaloniaNativeMenuPlugin
{
    public string SystemName => global::Highbyte.DotNet6502.Systems.Vic20.Vic20.SystemName;

    public int DisplayOrder => 20;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddTransient<Vic20MenuViewModel>();
        services.AddTransient<Vic20InfoViewModel>();
        services.AddTransient<Vic20ConfigDialogViewModel>();
        services.AddTransient<Vic20RomPromptService>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<Vic20MenuViewModel>();

    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<Vic20InfoViewModel>();

    public object? CreateConfigDialogContribution(IServiceProvider sp) => sp.GetService<Vic20ConfigDialogViewModel>();

    public ISystemMenuContributor? GetNativeMenuContributor(object? menuContribution)
        => menuContribution as ISystemMenuContributor;
}
