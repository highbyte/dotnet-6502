using System.Text.Json;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Vic20;
using Highbyte.DotNet6502.Systems.Vic20.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;

namespace Highbyte.DotNet6502.Impl.Avalonia.Vic20;

/// <summary>
/// VIC-20 system configurer for the Avalonia host.
/// Inherits all host-agnostic logic from <see cref="Vic20SystemConfigurerCore"/>
/// and wires the real Avalonia input handler.
/// </summary>
public class Vic20Setup : Vic20SystemConfigurerCore
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Func<string, string, string?, Task>? _saveCustomConfigString;

    public Vic20Setup(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        Func<string, string, string?, Task>? saveCustomConfigString = null)
        : base(loggerFactory, configuration, () => new Vic20HostConfig(), Vic20HostConfig.ConfigSectionName)
    {
        _loggerFactory = loggerFactory;
        _saveCustomConfigString = saveCustomConfigString;
    }

    public override async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (_saveCustomConfigString == null)
        {
            LoggerFactory.CreateLogger(nameof(Vic20Setup))
                .LogWarning("No method for saving custom config JSON supplied, so not saving Vic20HostConfig.");
            return;
        }
        var json = JsonSerializer.Serialize(hostSystemConfig, Vic20HostConfigJsonContext.Default.Vic20HostConfig);
        await _saveCustomConfigString(Vic20HostConfig.ConfigSectionName, json, null);
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var vic20 = (Vic20System)system;
        vic20.InputConsumer = new Vic20InputHandler(vic20, _loggerFactory);
        return Task.FromResult(new SystemRunner(vic20));
    }
}
