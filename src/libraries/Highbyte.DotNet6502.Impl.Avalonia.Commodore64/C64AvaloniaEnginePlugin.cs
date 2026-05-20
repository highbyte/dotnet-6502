using System;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Avalonia.Commodore64.C64AvaloniaEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Avalonia.Commodore64;

/// <summary>
/// Engine-side plugin for the C64 on the Avalonia + NAudio host pair. Registers the C64
/// <see cref="ISystemConfigurer{TIn,TAu}"/> (<see cref="C64Setup"/>) into DI.
/// </summary>
/// <remarks>
/// Avalonia's render pipeline is already system-agnostic, so — unlike the SilkNet engine plugin —
/// this contributes no render targets.
/// </remarks>
public sealed class C64AvaloniaEnginePlugin
    : ISystemEnginePlugin
{
    public string SystemName => C64.SystemName;

    public string HostTechName => "Avalonia.NAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Engine-side configurer for the Avalonia + NAudio host pair.
        // CustomConfigPersistence carries the host's "save custom config JSON" delegate via DI,
        // avoiding a dependency on AvaloniaHostApp (which is constructed after this is resolved).
        services.AddSingleton<ISystemConfigurer>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var config = sp.GetRequiredService<IConfiguration>();
            var persistence = sp.GetRequiredService<CustomConfigPersistence>();
            return new C64Setup(loggerFactory, config, persistence.Save);
        });
    }
}
