using Highbyte.DotNet6502.App.Avalonia.Shell.Oric.ViewModels;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Avalonia.Shell.Oric.OricAvaloniaShellPlugin))]

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Oric;

public sealed class OricAvaloniaShellPlugin : ISystemShellPlugin
{
    public string SystemName => global::Highbyte.DotNet6502.Systems.Oric.Oric.SystemName;
    public int DisplayOrder => 40;

    public void RegisterShellServices(IServiceCollection services)
    {
        services.AddTransient<OricMenuViewModel>();
        services.AddTransient<OricConfigDialogViewModel>();
        services.AddTransient<OricInfoViewModel>();
        services.AddTransient<OricRomPromptService>();
    }

    public object? CreateMenuContribution(IServiceProvider sp) => sp.GetService<OricMenuViewModel>();
    public object? CreateInfoContribution(IServiceProvider sp) => sp.GetService<OricInfoViewModel>();
    public object? CreateConfigDialogContribution(IServiceProvider sp) => sp.GetService<OricConfigDialogViewModel>();
}
