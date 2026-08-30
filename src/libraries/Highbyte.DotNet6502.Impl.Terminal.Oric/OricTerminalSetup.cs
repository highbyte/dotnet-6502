using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Impl.Terminal.Oric;

/// <summary>Oric Atmos configurer for the Terminal host, including keyboard and joystick input.</summary>
public sealed class OricTerminalSetup : OricSystemConfigurerCore
{
    public OricTerminalSetup(ILoggerFactory loggerFactory, IConfiguration configuration)
        : base(
            loggerFactory,
            configuration,
            () => new OricTerminalHostConfig(),
            OricTerminalHostConfig.ConfigSectionName)
    {
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var oric = (OricMachine)system;
        var terminalConfig = (OricTerminalHostConfig)hostSystemConfig;
        oric.InputConsumer = new OricInputHandler(oric, LoggerFactory, terminalConfig.InputConfig);
        return base.BuildSystemRunner(system, hostSystemConfig);
    }
}
