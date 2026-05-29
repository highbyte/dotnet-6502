using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64;

/// <summary>
/// C64 system configurer for the SilkNet + NAudio host. Everything system-agnostic comes from
/// <see cref="C64SystemConfigurerCore"/>; this only wires the SilkNet keyboard/joystick input
/// handler. See <c>docs/system-configurer-consolidation.md</c>.
/// </summary>
public class C64Setup : C64SystemConfigurerCore
{
    public C64Setup(ILoggerFactory loggerFactory, IConfiguration configuration)
        : base(loggerFactory, configuration, () => new C64HostConfig(), C64HostConfig.ConfigSectionName)
    {
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        var c64 = (C64)system;

        c64.InputConsumer = new C64InputHandler(c64, LoggerFactory, c64HostConfig.InputConfig);

        return base.BuildSystemRunner(system, hostSystemConfig);
    }
}
