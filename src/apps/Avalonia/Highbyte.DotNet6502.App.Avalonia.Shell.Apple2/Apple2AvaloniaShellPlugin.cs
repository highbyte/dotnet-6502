using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Shell.Apple2.ViewModels;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Avalonia.Shell.Apple2.Apple2AvaloniaShellPlugin))]

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Apple2;

/// <summary>
/// Shell-side plugin for the Apple II on the Avalonia host.
/// </summary>
/// <remarks>
/// The menu contribution is deliberately small — the Apple II has no PRG format, disk, tape or
/// joystick support, so unlike the C64 and VIC-20 menus there is nothing to load or save. Its
/// real job is to host the Configuration section, which is the only route to the ROM settings
/// dialog. The engine-side wiring (the <c>ISystemConfigurer</c>) lives in the engine plugin
/// <c>Apple2AvaloniaEnginePlugin</c> (Impl.Avalonia.Apple2).
/// </remarks>
public sealed class Apple2AvaloniaShellPlugin : ISystemShellPlugin, IAvaloniaNativeMenuPlugin
{
    public string SystemName => global::Highbyte.DotNet6502.Systems.Apple2.Apple2.SystemName;

    public int DisplayOrder => 30;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddTransient<Apple2MenuViewModel>();
        services.AddTransient<Apple2InfoViewModel>();
        services.AddTransient<Apple2ConfigDialogViewModel>();
        services.AddTransient<Apple2RomPromptService>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<Apple2MenuViewModel>();

    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<Apple2InfoViewModel>();

    public object? CreateConfigDialogContribution(IServiceProvider sp) => sp.GetService<Apple2ConfigDialogViewModel>();

    public ISystemMenuContributor? GetNativeMenuContributor(object? menuContribution)
        => menuContribution as ISystemMenuContributor;
}
