using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Highbyte.DotNet6502.Systems.Plugins;

/// <summary>
/// Engine-side plugin: ships in <c>Impl.&lt;Tech&gt;.&lt;System&gt;</c> libraries.
/// Adds the system's <see cref="ISystemConfigurer"/> (and supporting services) to the host app's
/// DI container.
/// </summary>
public interface ISystemEnginePlugin
{
    string SystemName { get; }

    /// <summary>
    /// Identifier for the host tech combination this plugin targets,
    /// e.g. "Avalonia.NAudio", "SilkNet.NAudio", "SadConsole.NAudio".
    /// Used for diagnostics and to disambiguate when the same SystemName
    /// has plugins for several hosts in scope.
    /// </summary>
    string HostTechName { get; }

    void Register(IServiceCollection services, IConfiguration configuration);
}
