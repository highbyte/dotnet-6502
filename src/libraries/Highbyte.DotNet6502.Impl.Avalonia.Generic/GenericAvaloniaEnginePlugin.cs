using System;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Avalonia.Generic.GenericAvaloniaEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.Avalonia.Generic;

/// <summary>
/// Engine-side plugin for the Generic computer on the Avalonia + NAudio host pair. Registers the
/// Generic <see cref="ISystemConfigurer{TIn,TAu}"/> (<see cref="GenericComputerSetup"/>) into DI.
/// The Generic example <c>.prg</c> files are embedded in this assembly (loaded by reflection).
/// </summary>
public sealed class GenericAvaloniaEnginePlugin
    : ISystemEnginePlugin
{
    public string SystemName => GenericComputer.SystemName;

    public string HostTechName => "Avalonia.NAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Engine-side configurer for the Avalonia + NAudio host pair.
        services.AddSingleton<ISystemConfigurer>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var config = sp.GetRequiredService<IConfiguration>();
            var persistence = sp.GetRequiredService<CustomConfigPersistence>();
            return new GenericComputerSetup(loggerFactory, config, persistence.Save);
        });
    }
}
