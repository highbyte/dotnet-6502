using Highbyte.DotNet6502.Impl.SadConsole.Generic.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SadConsole.Generic;

/// <summary>
/// Generic-computer system configurer for the SadConsole host. Everything system-agnostic comes
/// from <see cref="GenericComputerSystemConfigurerCore"/>; this only wires the SadConsole input
/// handler. See <c>docs/system-configurer-consolidation.md</c>.
/// </summary>
public class GenericComputerSetup : GenericComputerSystemConfigurerCore
{
    public GenericComputerSetup(ILoggerFactory loggerFactory, IConfiguration configuration)
        : base(loggerFactory, configuration, () => new GenericComputerHostConfig(), GenericComputerHostConfig.ConfigSectionName)
    {
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var genericComputer = (GenericComputer)system;

        genericComputer.InputConsumer = new GenericSadConsoleInputHandler(
            genericComputer, genericComputer.GenericComputerConfig.Memory.Input, LoggerFactory);

        return Task.FromResult(new SystemRunner(genericComputer));
    }
}
