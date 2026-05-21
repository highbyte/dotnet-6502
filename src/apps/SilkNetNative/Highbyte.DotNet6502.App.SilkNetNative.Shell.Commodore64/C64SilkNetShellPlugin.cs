using System;
using Highbyte.DotNet6502.App.SilkNetNative.Core;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.SilkNetNative.Shell.Commodore64.C64SilkNetShellPlugin))]

namespace Highbyte.DotNet6502.App.SilkNetNative.Shell.Commodore64;

/// <summary>
/// Shell-side plugin contributing the SilkNet/ImGui C64 menu to the host app's shell.
/// </summary>
/// <remarks>
/// The C64 engine-side wiring (the <c>ISystemConfigurer</c> and render targets) lives in the
/// engine plugin <c>C64SilkNetEnginePlugin</c> (Impl.SilkNet.Commodore64) — this shell plugin
/// is UI only. <see cref="CreateMenuContribution"/> returns an <see cref="IImGuiMenuContributor"/>
/// — the SilkNet host casts to that interface. <see cref="CreateInfoContribution"/> and
/// <see cref="CreateConfigDialogContribution"/> are null because the SilkNet shell folds info
/// and config into the menu (unlike Avalonia, which has separate panels/dialogs).
/// </remarks>
public sealed class C64SilkNetShellPlugin : ISystemShellPlugin
{
    public string SystemName => C64.SystemName;

    public void RegisterShellServices(IServiceCollection services)
    {
        // Per-system menu drawer — one instance for the app lifetime.
        services.AddSingleton<C64SilkNetImGuiMenu>(sp => new C64SilkNetImGuiMenu(
            sp.GetRequiredService<SilkNetHostApp>(),
            sp.GetRequiredService<ISilkNetMenuHost>(),
            sp.GetRequiredService<ILoggerFactory>()));
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<C64SilkNetImGuiMenu>();

    public object? CreateInfoContribution(IServiceProvider sp) => null;

    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
