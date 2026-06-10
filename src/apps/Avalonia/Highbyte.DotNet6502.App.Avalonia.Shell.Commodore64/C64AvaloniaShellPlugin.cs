using System;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.ViewModels;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.C64AvaloniaShellPlugin))]

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64;

/// <summary>
/// Shell-side plugin that contributes C64-specific UI (menu, info, config dialog)
/// to the Avalonia host app.
/// </summary>
/// <remarks>
/// The C64 engine-side wiring (the <c>ISystemConfigurer</c>) lives in the engine plugin
/// <c>C64AvaloniaEnginePlugin</c> (Impl.Avalonia.Commodore64) — this shell plugin is UI only.
/// </remarks>
public sealed class C64AvaloniaShellPlugin : ISystemShellPlugin, IAvaloniaNativeMenuPlugin
{
    public string SystemName => C64.SystemName;

    public int DisplayOrder => 10;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddTransient<C64MenuViewModel>();
        services.AddTransient<C64InfoViewModel>();
        services.AddTransient<C64ConfigDialogViewModel>();
        services.AddTransient<C64AcknowledgmentService>();

        // Automated-startup participant — resolved by the host (keyed by system name) and invoked
        // by AutomatedStartupHandler before Start(). See docs/automated-startup-abstraction.md.
        services.AddKeyedSingleton<IAutomatedStartupParticipant, C64AvaloniaStartupParticipant>(C64.SystemName);
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<C64MenuViewModel>();

    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<C64InfoViewModel>();

    public object? CreateConfigDialogContribution(IServiceProvider sp) => sp.GetService<C64ConfigDialogViewModel>();

    // C64MenuViewModel implements ISystemMenuContributor — its commands and shortcuts
    // populate the macOS native menu. Projecting the already-created menu contribution
    // (rather than resolving a fresh VM) keeps the native menu and the in-window panel
    // bound to the same instance.
    public ISystemMenuContributor? GetNativeMenuContributor(object? menuContribution)
        => menuContribution as ISystemMenuContributor;
}
