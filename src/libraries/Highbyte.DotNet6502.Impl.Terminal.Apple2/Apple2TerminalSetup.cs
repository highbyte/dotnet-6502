using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Apple2;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Impl.Terminal.Apple2;

/// <summary>Apple II configurer for the Terminal host, including keyboard and joystick input.</summary>
public class Apple2TerminalSetup : Apple2SystemConfigurerCore
{
    public Apple2TerminalSetup(ILoggerFactory loggerFactory, IConfiguration configuration)
        : base(loggerFactory, configuration, () => new Apple2TerminalHostConfig(),
            Apple2TerminalHostConfig.ConfigSectionName)
    {
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var apple2 = (Apple2System)system;
        var terminalConfig = (Apple2TerminalHostConfig)hostSystemConfig;

        terminalConfig.InputConfig.KeyboardJoystickEnabled =
            terminalConfig.SystemConfig.KeyboardJoystickEnabled;
        apple2.InputConsumer = new Apple2InputHandler(apple2, LoggerFactory, terminalConfig.InputConfig);

        return Task.FromResult(new SystemRunner(apple2));
    }
}
