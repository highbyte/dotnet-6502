using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Avalonia.Oric.OricAvaloniaEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Avalonia.Oric;

public sealed class OricAvaloniaEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => global::Highbyte.DotNet6502.Systems.Oric.Oric.SystemName;
    public string HostTechName => "Avalonia.NAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp => new OricSetup(
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<CustomConfigPersistence>().Save));
    }
}
