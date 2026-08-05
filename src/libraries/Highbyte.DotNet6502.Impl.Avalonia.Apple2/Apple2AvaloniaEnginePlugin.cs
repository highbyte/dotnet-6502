using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Avalonia.Apple2.Apple2AvaloniaEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Avalonia.Apple2;

/// <summary>
/// Engine-side plugin for the Apple II on the Avalonia + NAudio host pair.
/// Registers the <see cref="Apple2Setup"/> (the <see cref="ISystemConfigurer"/>) into DI.
/// </summary>
public sealed class Apple2AvaloniaEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => global::Highbyte.DotNet6502.Systems.Apple2.Apple2.SystemName;

    public string HostTechName => "Avalonia.NAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var config = sp.GetRequiredService<IConfiguration>();
            var persistence = sp.GetRequiredService<CustomConfigPersistence>();
            return new Apple2Setup(loggerFactory, config, persistence.Save);
        });
    }
}
