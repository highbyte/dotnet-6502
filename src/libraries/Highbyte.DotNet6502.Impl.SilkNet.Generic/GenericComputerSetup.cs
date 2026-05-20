using Highbyte.DotNet6502.Impl.SilkNet.Generic.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SilkNet.Generic;

/// <summary>
/// Generic-computer system configurer for the SilkNet host. Everything system-agnostic comes from
/// <see cref="GenericComputerSystemConfigurerCore"/>; this only wires the SilkNet input handler.
/// See <c>docs/system-configurer-consolidation.md</c>.
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

        genericComputer.InputConsumer = new GenericComputerSilkNetInputHandler(
            genericComputer, genericComputer.GenericComputerConfig.Memory.Input);

        return Task.FromResult(new SystemRunner(genericComputer));
    }
}
