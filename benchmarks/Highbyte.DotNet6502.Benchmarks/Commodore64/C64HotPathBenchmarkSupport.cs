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

    public static C64 CreateScenario(C64HotPathScenario scenario)
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
