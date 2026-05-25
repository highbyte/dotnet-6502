using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Vic20;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;

namespace Highbyte.DotNet6502.Impl.Avalonia.Vic20;

/// <summary>
/// VIC-20 system configurer for the Avalonia host. Inherits all host-agnostic logic from
/// <see cref="Vic20SystemConfigurerCore"/> and wires the Avalonia input stub.
/// </summary>
public class Vic20Setup : Vic20SystemConfigurerCore
{
    public Vic20Setup(ILoggerFactory loggerFactory, IConfiguration configuration)
        : base(loggerFactory, configuration, () => new Vic20HostConfig(), Vic20HostConfig.ConfigSectionName)
    {
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        // Step 4 (idea doc): keyboard handler stub — keys reach the system but are routed to /dev/null.
        // A real Avalonia input handler would be wired here once the plugin contract is fully proven.
        var vic20 = (Vic20System)system;
        vic20.InputConsumer = new NullInputConsumer(vic20);
        return Task.FromResult(new SystemRunner(vic20));
    }
}
