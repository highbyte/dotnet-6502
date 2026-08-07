using System.Text.Json;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Apple2;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Impl.Avalonia.Apple2;

/// <summary>
/// Apple II system configurer for the Avalonia host.
/// Inherits all host-agnostic logic from <see cref="Apple2SystemConfigurerCore"/>
/// and wires the input handler.
/// </summary>
public class Apple2Setup : Apple2SystemConfigurerCore
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Func<string, string, string?, Task>? _saveCustomConfigString;

    public Apple2Setup(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        Func<string, string, string?, Task>? saveCustomConfigString = null)
        : base(loggerFactory, configuration, () => new Apple2HostConfig(), Apple2HostConfig.ConfigSectionName)
    {
        _loggerFactory = loggerFactory;
        _saveCustomConfigString = saveCustomConfigString;
    }

    public override async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (_saveCustomConfigString == null)
        {
            LoggerFactory.CreateLogger(nameof(Apple2Setup))
                .LogWarning("No method for saving custom config JSON supplied, so not saving Apple2HostConfig.");
            return;
        }
        var json = JsonSerializer.Serialize(hostSystemConfig, Apple2HostConfigJsonContext.Default.Apple2HostConfig);
        await _saveCustomConfigString(Apple2HostConfig.ConfigSectionName, json, null);
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var apple2 = (Apple2System)system;
        var apple2HostConfig = (Apple2HostConfig)hostSystemConfig;

        // The persisted setting is the source of truth; the mapping object carries it at runtime so
        // the input handler has everything it needs in one place.
        apple2HostConfig.InputConfig.KeyboardJoystickEnabled =
            apple2HostConfig.SystemConfig.KeyboardJoystickEnabled;

        apple2.InputConsumer = new Apple2InputHandler(apple2, _loggerFactory, apple2HostConfig.InputConfig);
        return Task.FromResult(new SystemRunner(apple2));
    }
}
