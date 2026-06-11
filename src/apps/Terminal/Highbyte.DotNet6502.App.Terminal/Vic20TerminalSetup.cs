using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Vic20;
using Highbyte.DotNet6502.Systems.Vic20.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>
/// VIC-20 system configurer for the Terminal (TUI) host. Reuses the host-agnostic
/// <see cref="Vic20SystemConfigurerCore"/> and only adds the VIC-20 keyboard input handler.
/// </summary>
public class Vic20TerminalSetup : Vic20SystemConfigurerCore
{
    public Vic20TerminalSetup(ILoggerFactory loggerFactory, IConfiguration configuration)
        : base(loggerFactory, configuration, () => new Vic20TerminalHostConfig(), Vic20TerminalHostConfig.ConfigSectionName)
    {
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var vic20 = (Vic20System)system;
        vic20.InputConsumer = new Vic20InputHandler(vic20, LoggerFactory);
        return Task.FromResult(new SystemRunner(vic20));
    }
}
