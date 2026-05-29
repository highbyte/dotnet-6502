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

    public Vic20Setup(ILoggerFactory loggerFactory, IConfiguration configuration)
        : base(loggerFactory, configuration, () => new Vic20HostConfig(), Vic20HostConfig.ConfigSectionName)
    {
        _loggerFactory = loggerFactory;
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var vic20 = (Vic20System)system;
        vic20.InputConsumer = new Vic20InputHandler(vic20, _loggerFactory);
        return Task.FromResult(new SystemRunner(vic20));
    }
}
