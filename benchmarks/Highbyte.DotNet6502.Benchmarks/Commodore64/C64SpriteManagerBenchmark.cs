using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Highbyte.DotNet6502.Benchmarks.Commodore64.Data;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64;

[MemoryDiagnoser] // Memory diagnoser is used to measure memory allocations
//[ShortRunJob] // WARNING: ShortRunJob is a custom job runs faster than normal, but is less accurate.
//[DryJob] // DANGER: DryJob is a custom job that runs very quickly, but VERY INACCURATE. Use only to verify that benchmarks actual runs.
public class C64SpriteManagerBenchmark
{
    private C64 _c64 = default!;

    public IVic2SpriteManager Vic2SpriteManager => _c64.Vic2.SpriteManager;

    [Params(8)]
    //[Params(1, 2, 4, 8)]
    public int NumberOfSprites;

    // GlobalSetup is executed once, or if Params are used: once per each Params value combination
    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("// " + "GlobalSetup");

        var c64Config = new C64Config
        {
            C64Model = "C64NTSC",   // C64NTSC, C64PAL
            Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
            LoadROMs = false,
            TimerMode = TimerMode.UpdateEachRasterLine,
            AudioEnabled = true,
            InstrumentationEnabled = false
        };
        _c64 = C64.BuildC64(c64Config, new NullLoggerFactory());

        // ------------------------------------
        // Create test sprites
        // ------------------------------------
        var spriteGenerator = new C64SpriteGenerator(_c64);
        var spriteDataList = new Dictionary<int, (int x, int y, bool doubleWidth, bool doubleHeight, bool multiColor, byte spritePointer)>
        {
            { 0, (0, 0, false, true, false, 192) },
            { 1, (9, 0, true, false, false, 193) },
            { 2, (0, 9, false, true, false, 194) },
            { 3, (9, 9, true, true, false, 195) },
            { 4, (100, 100, false, true, true, 196) },
            { 5, (110, 100, true, false, false, 197) },
            { 6, (Vic2SpriteManager.ScreenOffsetX + 0, Vic2SpriteManager.ScreenOffsetY + 0, false, true, true, 198) },
            { 7, (300, 220, true, true, false, 199) }
        };
        // Loop each sprite
        for (var i = 0; i < NumberOfSprites; i++)
        {
            var spriteData = spriteDataList[i];
            byte[] spriteShape = spriteData.multiColor ?
                                    spriteGenerator.CreateTestMultiColorSpriteImage() :
                                    spriteGenerator.CreateTestSingleColorSpriteImage();

            spriteGenerator.CreateSprite(
                spriteNumber: (byte)i,
                x: (byte)spriteData.x,
                y: (byte)spriteData.y,
                doubleWidth: spriteData.doubleWidth,
                doubleHeight: spriteData.doubleHeight,
                multiColor: spriteData.multiColor,
                spriteShape: spriteShape,
                spritePointer: spriteData.spritePointer
            );
        }

        // ------------------------------------
        // Write characters to screen
        // ------------------------------------
        var charGenerator = new C64CharGenerator(_c64);
        charGenerator.CreateCharData();
        byte characterCode = 1; // A
        charGenerator.WriteToScreen(characterCode, 0, 0);
    }

    //[GlobalCleanup]
    //public void GlobalCleanup()
    //{
    //    Console.WriteLine("// " + "GlobalCleanup");
    //}

    [Benchmark]
    public void GetSpriteToSpriteCollissions()
    {
        var spriteToSpriteCollision = Vic2SpriteManager.GetSpriteToSpriteCollision();
    }

    [Benchmark]
    public void GetSpriteToBackgroundCollissions()
    {
        var spriteToBackgroundCollision = Vic2SpriteManager.GetSpriteToBackgroundCollision();
    }
}
