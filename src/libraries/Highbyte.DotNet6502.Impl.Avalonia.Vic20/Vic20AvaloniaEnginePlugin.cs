using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Plugins;
using Highbyte.DotNet6502.Systems.Vic20;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Avalonia.Vic20.Vic20AvaloniaEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Avalonia.Vic20;

/// <summary>
/// Engine-side plugin for the VIC-20 on the Avalonia + NAudio host pair.
/// Registers the <see cref="Vic20Setup"/> (the <see cref="ISystemConfigurer"/>) into DI.
/// </summary>
public sealed class Vic20AvaloniaEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => global::Highbyte.DotNet6502.Systems.Vic20.Vic20.SystemName;

    public string HostTechName => "Avalonia.NAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var config = sp.GetRequiredService<IConfiguration>();
            return new Vic20Setup(loggerFactory, config);
        });
    }
}
