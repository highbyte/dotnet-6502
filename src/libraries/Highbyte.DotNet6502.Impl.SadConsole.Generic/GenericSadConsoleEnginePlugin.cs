using System;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.SadConsole.Generic.GenericSadConsoleEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.SadConsole.Generic;

/// <summary>
/// Engine-side plugin for the Generic computer on the SadConsole + NAudio host pair. Registers the
/// Generic <see cref="ISystemConfigurer{TIn,TAu}"/> (<see cref="GenericComputerSetup"/>) into DI.
/// </summary>
public sealed class GenericSadConsoleEnginePlugin
    : ISystemEnginePlugin
{
    public string SystemName => GenericComputer.SystemName;

    public string HostTechName => "SadConsole.NAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Engine-side configurer for the SadConsole + NAudio host pair.
        services.AddSingleton<ISystemConfigurer>(sp =>
            new GenericComputerSetup(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>()));
    }
}
