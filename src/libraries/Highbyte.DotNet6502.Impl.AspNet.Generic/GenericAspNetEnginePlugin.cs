using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.AspNet.Generic.GenericAspNetEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.AspNet.Generic;

/// <summary>
/// Engine-side plugin for the Generic computer on the WASM (Blazor) + WebAudio host pair.
/// Registers the Generic <see cref="ISystemConfigurer{TIn,TAu}"/> (<see cref="GenericComputerSetup"/>)
/// into DI. Contributes no render targets — the Generic computer uses only the host's
/// system-agnostic targets.
/// </summary>
public sealed class GenericAspNetEnginePlugin
    : ISystemEnginePlugin
{
    public string SystemName => GenericComputer.SystemName;

    public string HostTechName => "WASM.WebAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Engine-side configurer for the WASM + WebAudio host pair.
        services.AddScoped<ISystemConfigurer>(sp =>
            new GenericComputerSetup(
                sp.GetRequiredService<BrowserContext>(),
                sp.GetRequiredService<ILoggerFactory>()));
    }
}
