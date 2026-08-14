using System;
using Highbyte.DotNet6502.App.Avalonia.Shell.Generic.ViewModels;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Avalonia.Shell.Generic.GenericComputerAvaloniaShellPlugin))]

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Generic;

/// <summary>
/// Shell-side plugin for the Generic computer on the Avalonia host.
/// </summary>
/// <remarks>
/// The Generic computer has no media (no PRG/disk/tape), so its menu is minimal: a
/// Configuration section opening the config dialog (CPU model and compatibility
/// profile). The info contribution stays null. The engine-side wiring (the
/// <c>ISystemConfigurer</c>) lives in the engine plugin
/// <c>GenericAvaloniaEnginePlugin</c> (Impl.Avalonia.Generic).
/// </remarks>
public sealed class GenericComputerAvaloniaShellPlugin : ISystemShellPlugin
{
    public string SystemName => GenericComputer.SystemName;

    public int DisplayOrder => 100;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddTransient<GenericComputerMenuViewModel>();
        services.AddTransient<GenericComputerConfigDialogViewModel>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<GenericComputerMenuViewModel>();

    public object? CreateInfoContribution(IServiceProvider sp) => null;

    public object? CreateConfigDialogContribution(IServiceProvider sp) => sp.GetService<GenericComputerConfigDialogViewModel>();
}
