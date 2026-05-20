using System;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Avalonia.Shell.Generic.GenericComputerAvaloniaShellPlugin))]

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Generic;

/// <summary>
/// Shell-side plugin for the Generic computer on the Avalonia host.
/// </summary>
/// <remarks>
/// The Generic computer has no system-specific UI surfaces, so this plugin contributes nothing —
/// the menu, info and config dialog contributions are all null and the host renders only its
/// system-agnostic chrome. The engine-side wiring (the <c>ISystemConfigurer</c>) lives in the
/// engine plugin <c>GenericAvaloniaEnginePlugin</c> (Impl.Avalonia.Generic). The plugin is kept
/// (rather than deleted) so the Generic system has a discoverable shell-side presence.
/// </remarks>
public sealed class GenericComputerAvaloniaShellPlugin : ISystemShellPlugin
{
    public string SystemName => GenericComputer.SystemName;

    public void RegisterShellServices(IServiceCollection services)
    {
        // No UI services — the Generic computer has no Avalonia-specific shell UI.
    }

    public object? CreateMenuContribution(IServiceProvider sp) => null;

    public object? CreateInfoContribution(IServiceProvider sp) => null;

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
