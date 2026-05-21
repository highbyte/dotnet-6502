using System;
using Highbyte.DotNet6502.App.SilkNetNative.Core;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.SilkNetNative.Shell.Generic.GenericSilkNetShellPlugin))]

namespace Highbyte.DotNet6502.App.SilkNetNative.Shell.Generic;

/// <summary>
/// Shell-side plugin contributing the SilkNet/ImGui Generic-computer menu to the host app's shell.
/// </summary>
/// <remarks>
/// The Generic engine-side wiring (the <c>ISystemConfigurer</c>) lives in the engine plugin
/// <c>GenericSilkNetEnginePlugin</c> (Impl.SilkNet.Generic) — this shell plugin is UI only.
/// <see cref="CreateMenuContribution"/> returns an <see cref="IImGuiMenuContributor"/>.
/// <see cref="CreateInfoContribution"/> and <see cref="CreateConfigDialogContribution"/> are
/// null because the SilkNet shell folds config into the menu.
/// </remarks>
public sealed class GenericSilkNetShellPlugin : ISystemShellPlugin
{
    public string SystemName => GenericComputer.SystemName;

    public void RegisterShellServices(IServiceCollection services)
    {
        // Per-system menu drawer — one instance for the app lifetime.
        services.AddSingleton<GenericSilkNetImGuiMenu>(sp => new GenericSilkNetImGuiMenu(
            sp.GetRequiredService<SilkNetHostApp>()));
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<GenericSilkNetImGuiMenu>();

    public object? CreateInfoContribution(IServiceProvider sp) => null;

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
