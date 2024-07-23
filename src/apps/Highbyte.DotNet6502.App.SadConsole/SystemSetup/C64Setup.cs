using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Input;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Video;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class C64Setup : SystemConfigurer<SadConsoleRenderContext, SadConsoleInputHandlerContext, NullAudioHandlerContext>
{
    public string SystemName => C64.SystemName;

    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly C64HostConfig _c64HostConfig;

    public C64Setup(ILoggerFactory loggerFactory, IConfiguration configuration, C64HostConfig c64HostConfig)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
        _c64HostConfig = c64HostConfig;
    }

    public Task<ISystemConfig> GetNewConfig(string configurationVariant)
    {
        var c64Config = new C64Config() { ROMs = new() };
        _configuration.GetSection(C64Config.ConfigSectionName).Bind(c64Config);
        return Task.FromResult<ISystemConfig>(c64Config);

        //var c64Config = new C64Config
        //{
        //    C64Model = "C64NTSC",   // C64NTSC, C64PAL
        //    Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
        //    //C64Model = "C64PAL",   // C64NTSC, C64PAL
        //    //Vic2Model = "PAL",     // NTSC, NTSC_old, PAL

        //    //ROMDirectory = "%USERPROFILE%/Documents/C64/VICE/C64",
        //    ROMDirectory = "%HOME%/Downloads/C64",
        //    ROMs = new List<ROM>
        //    {
        //        new ROM
        //        {
        //            Name = C64Config.BASIC_ROM_NAME,
        //            File = "basic.901226-01.bin",
        //            Data = null,
        //            Checksum = "79015323128650c742a3694c9429aa91f355905e",
        //        },
        //        new ROM
        //        {
        //            Name = C64Config.CHARGEN_ROM_NAME,
        //            File = "characters.901225-01.bin",
        //            Data = null,
        //            Checksum = "adc7c31e18c7c7413d54802ef2f4193da14711aa",
        //        },
        //        new ROM
        //        {
        //            Name = C64Config.KERNAL_ROM_NAME,
        //            File = "kernal.901227-03.bin",
        //            Data = null,
        //            Checksum = "1d503e56df85a62fee696e7618dc5b4e781df1bb",
        //        }
        //    },

        //    AudioSupported = false,
        //    AudioEnabled = false,

        //    InstrumentationEnabled = false, // Start with instrumentation off by default
        //};

        ////c64Config.Validate();
        //return Task.FromResult<ISystemConfig>(c64Config);
    }

    public Task PersistConfig(ISystemConfig systemConfig)
    {
        var c64Config = (C64Config)systemConfig;
        // TODO: Persist settings to file

        return Task.CompletedTask;
    }

    public ISystem BuildSystem(ISystemConfig systemConfig)
    {
        var c64Config = (C64Config)systemConfig;
        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return c64;
    }

    public Task<IHostSystemConfig> GetHostSystemConfig()
    {
        return Task.FromResult((IHostSystemConfig)_c64HostConfig);
    }

    public SystemRunner BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        IHostSystemConfig hostSystemConfig,
        SadConsoleRenderContext renderContext,
        SadConsoleInputHandlerContext inputHandlerContext,
        NullAudioHandlerContext audioHandlerContext
        )
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        var c64 = (C64)system;

        var renderer = new C64SadConsoleRenderer(c64, renderContext);
        var inputHandler = new C64SadConsoleInputHandler(c64, inputHandlerContext, _loggerFactory);
        var audioHandler = new NullAudioHandler(c64);

        return new SystemRunner(c64, renderer, inputHandler, audioHandler);
    }
}
