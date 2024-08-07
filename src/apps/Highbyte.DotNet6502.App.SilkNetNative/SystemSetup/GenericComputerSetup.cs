using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Generic.Input;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Generic.Video;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;

public class GenericComputerSetup : ISystemConfigurer<SilkNetRenderContextContainer, SilkNetInputHandlerContext, NAudioAudioHandlerContext>
{
    public string SystemName => GenericComputer.SystemName;

    private readonly ILoggerFactory _loggerFactory;
    private readonly GenericComputerHostConfig _genericComputerHostConfig;

    public GenericComputerSetup(ILoggerFactory loggerFactory, GenericComputerHostConfig genericComputerHostConfig)
    {
        _loggerFactory = loggerFactory;
        _genericComputerHostConfig = genericComputerHostConfig;
    }

    public Task<ISystemConfig> GetNewConfig(string configurationVariant)
    {
        var genericComputerConfig = new GenericComputerConfig
        {
            ProgramBinaryFile = "../../../../../../samples/Assembler/Generic/Build/hostinteraction_scroll_text_and_cycle_colors.prg",
            //ProgramBinaryFile = "%HOME%/source/repos/dotnet-6502/samples/Assembler/Generic/Build/hostinteraction_scroll_text_and_cycle_colors.prg",
            CPUCyclesPerFrame = 8000,
            Memory = new EmulatorMemoryConfig
            {
                Screen = new EmulatorScreenConfig
                {
                    Cols = 40,
                    Rows = 25,
                    BorderCols = 3,
                    BorderRows = 3,
                    UseAscIICharacters = true,
                    DefaultBgColor = 0x00,     // 0x00 = Black (C64 scheme)
                    DefaultFgColor = 0x01,     // 0x0f = Light grey, 0x0e = Light Blue, 0x01 = White  (C64 scheme)
                    DefaultBorderColor = 0x0b, // 0x0b = Dark grey (C64 scheme)
                },
                Input = new EmulatorInputConfig
                {
                    KeyPressedAddress = 0xd030,
                    KeyDownAddress = 0xd031,
                    KeyReleasedAddress = 0xd031,
                }
            }
        };

        genericComputerConfig.Validate();

        return Task.FromResult<ISystemConfig>(genericComputerConfig);
    }

    public Task PersistConfig(ISystemConfig systemConfig)
    {
        var genericComputerConfig = (GenericComputerConfig)systemConfig;
        // TODO: Save config settings to file
        return Task.CompletedTask;
    }

    public ISystem BuildSystem(ISystemConfig systemConfig)
    {
        var genericComputerConfig = (GenericComputerConfig)systemConfig;
        return GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig, _loggerFactory);
    }

    public Task<IHostSystemConfig> GetHostSystemConfig()
    {
        return Task.FromResult((IHostSystemConfig)_genericComputerHostConfig);
    }

    public SystemRunner BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        IHostSystemConfig hostSystemConfig,
        SilkNetRenderContextContainer renderContext,
        SilkNetInputHandlerContext inputHandlerContext,
        NAudioAudioHandlerContext audioHandlerContext)
    {
        var genericComputer = (GenericComputer)system;
        var genericComputerConfig = (GenericComputerConfig)systemConfig;

        var renderer = new GenericComputerSkiaRenderer(genericComputer, renderContext.SkiaRenderContext, genericComputerConfig.Memory.Screen);
        var inputHandler = new GenericComputerSilkNetInputHandler(genericComputer, inputHandlerContext, genericComputerConfig.Memory.Input);
        var audioHandler = new NullAudioHandler(genericComputer);

        return new SystemRunner(genericComputer, renderer, inputHandler, audioHandler);
    }
}
