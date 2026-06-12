using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Terminal.Commodore64;

/// <summary>
/// C64 system configurer for the Terminal (TUI) host. Reuses the host-agnostic
/// <see cref="C64SystemConfigurerCore"/> and only adds the C64 keyboard input handler (without the
/// AI BASIC coding assistant, which is desktop-only).
/// </summary>
public class C64TerminalSetup : C64SystemConfigurerCore
{
    public C64TerminalSetup(ILoggerFactory loggerFactory, IConfiguration configuration)
        : base(loggerFactory, configuration, () => new C64TerminalHostConfig(), C64TerminalHostConfig.ConfigSectionName)
    {
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var c64 = (C64)system;

        // Attach the C64 keyboard input handler (no coding assistant in the terminal host).
        c64.InputConsumer = new C64InputHandler(c64, LoggerFactory, new C64InputConfig());

        return base.BuildSystemRunner(system, hostSystemConfig);
    }
}
