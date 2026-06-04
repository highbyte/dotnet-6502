using System.Runtime.InteropServices;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using Highbyte.DotNet6502.Benchmarks.Commodore64.Data;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64;

public enum C64HotPathScenario
{
    CoreOnly,
    RenderOnly,
    AudioOnly,
    RenderAndAudio,
}

public enum C64SpriteScenario
{
    None,
    MixedVisibleSprites,
}

internal sealed class C64HotPathConfig : ManualConfig
{
    public C64HotPathConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);
        AddJob(Job.ShortRun);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(maxDepth: 2, exportHtml: true)));
        }
    }
}

internal static class C64HotPathBenchmarkSupport
{
    public const ushort StartAddress = 0xC000;

    public static C64 CreateScenario(C64HotPathScenario scenario, C64SpriteScenario spriteScenario = C64SpriteScenario.None)
    {
        var c64Config = new C64Config
        {
            C64Model = "C64NTSC",
            Vic2Model = "NTSC",
            LoadROMs = false,
            TimerMode = TimerMode.UpdateEachRasterLine,
            AudioEnabled = UsesSampleAudioProvider(scenario),
            AudioProviderType = UsesSampleAudioProvider(scenario) ? typeof(C64SidSampleProvider) : null,
            RenderProviderType = UsesRasterizer(scenario) ? typeof(Vic2Rasterizer) : null,
            InstrumentationEnabled = false
        };

        var c64 = C64.BuildC64(c64Config, new NullLoggerFactory());
        SeedVisibleScreen(c64);
        SeedSprites(c64, spriteScenario);
        LoadProgram(c64.Mem, StartAddress);
        ConfigureSampleAudio(c64);
        return c64;
    }

    private static bool UsesRasterizer(C64HotPathScenario scenario)
        => scenario is C64HotPathScenario.RenderOnly or C64HotPathScenario.RenderAndAudio;

    private static bool UsesSampleAudioProvider(C64HotPathScenario scenario)
        => scenario is C64HotPathScenario.AudioOnly or C64HotPathScenario.RenderAndAudio;

    private static void SeedVisibleScreen(C64 c64)
    {
        var charGenerator = new C64CharGenerator(c64);
        charGenerator.CreateCharData();

        c64.Mem[Vic2Addr.BORDER_COLOR] = 0x06;
        c64.Mem[Vic2Addr.BACKGROUND_COLOR_0] = 0x0E;

        var screenBase = c64.Vic2.VideoMatrixBaseAddress;
        var textCols = c64.Vic2.Vic2Screen.TextCols;
        var textRows = c64.Vic2.Vic2Screen.TextRows;

        for (int row = 0; row < textRows; row++)
        {
            for (int col = 0; col < textCols; col++)
            {
                int offset = (row * textCols) + col;
                c64.Mem[(ushort)(screenBase + offset)] = 0x01;
                c64.Mem[(ushort)(Vic2Addr.COLOR_RAM_START + offset)] = (byte)((offset % 15) + 1);
            }
        }
    }

    private static void SeedSprites(C64 c64, C64SpriteScenario spriteScenario)
    {
        if (spriteScenario == C64SpriteScenario.None)
            return;

        var spriteGenerator = new C64SpriteGenerator(c64);
        var spriteManager = c64.Vic2.SpriteManager;

        c64.WriteIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_0, 0x05);
        c64.WriteIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_1, 0x0D);

        var spriteSpecs = new (int X, int Y, bool DoubleWidth, bool DoubleHeight, bool MultiColor, bool BehindForeground, byte Color, byte Pointer, Func<C64SpriteGenerator, byte[]> ShapeFactory)[]
        {
            (spriteManager.ScreenOffsetX + 0,   spriteManager.ScreenOffsetY + 0,   false, false, false, false, 0x01, 192, g => g.CreateTestSingleColorSpriteImage()),
            (spriteManager.ScreenOffsetX + 24,  spriteManager.ScreenOffsetY + 8,   true,  false, false, true,  0x02, 193, g => g.CreateTestSingleColorSpriteImage2()),
            (spriteManager.ScreenOffsetX + 56,  spriteManager.ScreenOffsetY + 18,  false, true,  false, false, 0x03, 194, g => g.CreateTestSingleColorSpriteImage()),
            (spriteManager.ScreenOffsetX + 88,  spriteManager.ScreenOffsetY + 28,  true,  true,  false, true,  0x04, 195, g => g.CreateTestSingleColorSpriteImage2()),
            (spriteManager.ScreenOffsetX + 124, spriteManager.ScreenOffsetY + 44,  false, false, true,  false, 0x06, 196, g => g.CreateTestMultiColorSpriteImage()),
            (spriteManager.ScreenOffsetX + 156, spriteManager.ScreenOffsetY + 60,  true,  false, true,  true,  0x07, 197, g => g.CreateTestMultiColorSpriteImage()),
            (spriteManager.ScreenOffsetX + 188, spriteManager.ScreenOffsetY + 84,  false, true,  true,  false, 0x08, 198, g => g.CreateTestMultiColorSpriteImage()),
            (spriteManager.ScreenOffsetX + 216, spriteManager.ScreenOffsetY + 104, true,  true,  true,  true,  0x09, 199, g => g.CreateTestMultiColorSpriteImage()),
        };

        byte spriteForegroundPriority = 0;

        for (int spriteNumber = 0; spriteNumber < spriteSpecs.Length; spriteNumber++)
        {
            var sprite = spriteSpecs[spriteNumber];
            spriteGenerator.CreateSprite(
                spriteNumber: spriteNumber,
                x: (byte)sprite.X,
                y: (byte)sprite.Y,
                doubleWidth: sprite.DoubleWidth,
                doubleHeight: sprite.DoubleHeight,
                multiColor: sprite.MultiColor,
                spriteShape: sprite.ShapeFactory(spriteGenerator),
                spritePointer: sprite.Pointer);

            c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_COLOR + spriteNumber), sprite.Color);
            spriteForegroundPriority.ChangeBit(spriteNumber, sprite.BehindForeground);
        }

        c64.WriteIOStorage(Vic2Addr.SPRITE_FOREGROUND_PRIO, spriteForegroundPriority);
        spriteManager.SetAllDirty();
    }

    private static void ConfigureSampleAudio(C64 c64)
    {
        if (c64.AudioProvider is not C64SidSampleProvider sampleProvider)
            return;

        sampleProvider.Init(AcceptAllSamples);

        c64.Mem[SidAddr.FRELO1] = 0x45;
        c64.Mem[SidAddr.FREHI1] = 0x1D;
        c64.Mem[SidAddr.ATDCY1] = 0x00;
        c64.Mem[SidAddr.SUREL1] = 0xF0;
        c64.Mem[SidAddr.SIGVOL] = 0x0F;
        c64.Mem[SidAddr.VCREG1] = 0x21;
    }

    private static int AcceptAllSamples(ReadOnlySpan<float> samples) => samples.Length;

    private static void LoadProgram(Memory mem, ushort startAddress)
    {
        var address = startAddress;
        mem.WriteByte(ref address, OpCodeId.CLC);
        mem.WriteByte(ref address, OpCodeId.LDA_I);
        mem.WriteByte(ref address, 0x11);
        mem.WriteByte(ref address, OpCodeId.ADC_I);
        mem.WriteByte(ref address, 0x07);
        mem.WriteByte(ref address, OpCodeId.TAX);
        mem.WriteByte(ref address, OpCodeId.INX);
        mem.WriteByte(ref address, OpCodeId.DEX);
        mem.WriteByte(ref address, OpCodeId.EOR_I);
        mem.WriteByte(ref address, 0x55);
        mem.WriteByte(ref address, OpCodeId.JMP_ABS);
        mem.WriteWord(ref address, startAddress);
    }
}
