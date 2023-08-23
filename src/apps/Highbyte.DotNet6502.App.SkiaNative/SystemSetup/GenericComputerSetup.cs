using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Generic.Input;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Generic.Video;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SkiaNative.SystemSetup;

public class GenericComputerSetup : SystemConfigurer<SkiaRenderContext, SilkNetInputHandlerContext, NAudioAudioHandlerContext>
{
    public string SystemName => GenericComputer.SystemName;

    private readonly ILoggerFactory _loggerFactory;

    public GenericComputerSetup(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public async Task<ISystemConfig> GetNewConfig(string configurationVariant)
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

        return genericComputerConfig;
    }

    public async Task PersistConfig(ISystemConfig systemConfig)
    {
        var genericComputerConfig = (GenericComputerConfig)systemConfig;
        // TODO: Save config settings to file
    }

    public ISystem BuildSystem(ISystemConfig systemConfig)
    {
        var genericComputerConfig = (GenericComputerConfig)systemConfig;
        return GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig, _loggerFactory);
    }

    public SystemRunner BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        SkiaRenderContext skiaRenderContext,
        SilkNetInputHandlerContext inputHandlerContext,
        NAudioAudioHandlerContext audioHandlerContext)
    {
        var genericComputerConfig = (GenericComputerConfig)systemConfig;

        var renderer = new GenericComputerSkiaRenderer(genericComputerConfig.Memory.Screen);
        var inputHandler = new GenericComputerSilkNetInputHandler(genericComputerConfig.Memory.Input);
        var audioHandler = new NullAudioHandler();

        var genericComputer = (GenericComputer)system;

        renderer.Init(genericComputer, skiaRenderContext);
        inputHandler.Init(genericComputer, inputHandlerContext);

        var systemRunnerBuilder = new SystemRunnerBuilder<GenericComputer, SkiaRenderContext, SilkNetInputHandlerContext, NullAudioHandlerContext>(genericComputer);
        var systemRunner = systemRunnerBuilder
            .WithRenderer(renderer)
            .WithInputHandler(inputHandler)
            .WithAudioHandler(audioHandler)
            .Build();
        return systemRunner;
    }
}
