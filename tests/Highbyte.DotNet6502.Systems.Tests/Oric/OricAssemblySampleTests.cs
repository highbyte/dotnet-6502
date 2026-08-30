using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Render;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricAssemblySampleTests
{
    private const string RasterBarsTapPath =
        "../../../../../samples/Assembler/Oric/Raster/Build/vsync_raster_bars.tap";
    private const string TimerRasterBarsTapPath =
        "../../../../../samples/Assembler/Oric/Raster/Build/timer1_raster_bars.tap";

    [Fact]
    public void VSyncRasterBarsRaceTheBeamFollowSineWaveAndRestoreVideoMemory()
    {
        var tapFile = OricTapParser.Parse(File.ReadAllBytes(RasterBarsTapPath));
        Assert.Equal("RASTERBARS", tapFile.Name);
        Assert.True(tapFile.IsMachineCode);
        Assert.True(tapFile.IsAutoRun);
        Assert.Equal(0x0600, tapFile.StartAddress);

        var oric = new OricMachine(
            new OricConfig { AudioEnabled = false, VSyncHackEnabled = true },
            NullLoggerFactory.Instance);
        var standardCharset = Enumerable.Range(0, 0x400)
            .Select(index => (byte)(index * 37 + 11))
            .ToArray();
        oric.Mem.StoreData(OricRasterizer.TextStandardCharsetAddress, standardCharset);
        oric.Mem.StoreData(tapFile.StartAddress, tapFile.Data);
        oric.Reset(tapFile.StartAddress);

        for (var frame = 0; frame < 10; frame++)
            oric.ExecuteOneFrame();

        var rasterizer = Assert.IsType<OricRasterizer>(oric.RenderProvider);
        var pixels = rasterizer.CurrentFrontBuffer.Span;
        var blue = OricRasterizer.PackBgra(0xff, 0x00, 0x00, 0xff);
        var firstPixelColours = new HashSet<uint>();
        for (var y = 0; y < OricConfig.HiResHeight; y++)
            firstPixelColours.Add(pixels[y * OricConfig.VisibleWidth]);

        Assert.Contains(blue, firstPixelColours);
        Assert.True(firstPixelColours.Count >= 5, "Expected the blue background and several raster-bar colours.");

        Assert.Equal(
            standardCharset,
            oric.Mem.ReadData(OricRasterizer.HiResStandardCharsetAddress, (ushort)standardCharset.Length));

        var barTopLines = new List<int>();
        for (var frame = 0; frame < 256; frame++)
        {
            oric.ExecuteOneFrame();
            barTopLines.Add(FindBarTopLine(rasterizer, blue));
        }

        Assert.Equal(8, barTopLines.Min());
        Assert.Equal(184, barTopLines.Max());
        var lineMovements = barTopLines.Zip(barTopLines.Skip(1), (current, next) => next - current);
        Assert.Contains(lineMovements, movement => movement > 0);
        Assert.Contains(lineMovements, movement => movement < 0);
        Assert.Contains(lineMovements, movement => movement == 0);

        for (var y = 0; y < OricConfig.HiResHeight; y++)
        {
            Assert.Equal(
                0x14,
                oric.Mem[(ushort)(OricRasterizer.HiResScreenAddress + y * OricConfig.Columns)]);
        }
    }

    [Fact]
    public void Timer1RasterBarsWorkWithoutCb1AndRequireProgressiveRendering()
    {
        var tapFile = OricTapParser.Parse(File.ReadAllBytes(TimerRasterBarsTapPath));
        Assert.Equal("TIMERBARS", tapFile.Name);
        Assert.True(tapFile.IsMachineCode);
        Assert.True(tapFile.IsAutoRun);
        Assert.Equal(0x0900, tapFile.StartAddress);

        var oric = new OricMachine(
            new OricConfig { AudioEnabled = false, VSyncHackEnabled = false },
            NullLoggerFactory.Instance);
        oric.Mem.StoreData(tapFile.StartAddress, tapFile.Data);
        oric.Reset(tapFile.StartAddress);

        var rasterizer = Assert.IsType<OricRasterizer>(oric.RenderProvider);
        var cyan = OricRasterizer.PackBgra(0xff, 0xff, 0x00, 0xff);
        var progressivelyCapturedBandLines = new List<int>();

        for (var frame = 0; frame < 400; frame++)
        {
            oric.ExecuteOneFrame();
            var cyanLine = FindFirstLineWithColour(rasterizer, cyan);
            if (cyanLine >= 0 && AreAllTextRowsBlueInMemory(oric))
                progressivelyCapturedBandLines.Add(cyanLine);
        }

        Assert.True(
            progressivelyCapturedBandLines.Count >= 20,
            "Expected cyan bands in completed frames whose end-of-frame memory had already returned to blue.");
        Assert.True(
            progressivelyCapturedBandLines.Distinct().Count() >= 10,
            "Expected the deliberately offset Timer 1 phase to move the cyan band through the display.");
    }

    private static int FindBarTopLine(OricRasterizer rasterizer, uint backgroundColour)
    {
        var pixels = rasterizer.CurrentFrontBuffer.Span;
        for (var y = 0; y < OricConfig.HiResHeight; y++)
        {
            if (pixels[y * OricConfig.VisibleWidth] != backgroundColour)
                return y;
        }

        throw new Xunit.Sdk.XunitException("Raster bar was not visible in the completed frame.");
    }

    private static int FindFirstLineWithColour(OricRasterizer rasterizer, uint colour)
    {
        var pixels = rasterizer.CurrentFrontBuffer.Span;
        for (var y = 0; y < OricConfig.VisibleHeight; y++)
        {
            if (pixels[y * OricConfig.VisibleWidth] == colour)
                return y;
        }

        return -1;
    }

    private static bool AreAllTextRowsBlueInMemory(OricMachine oric)
    {
        for (var row = 0; row < OricConfig.VisibleHeight / OricConfig.CharacterHeight; row++)
        {
            if (oric.Mem[(ushort)(OricRasterizer.TextScreenAddress + row * OricConfig.Columns)] != 0x14)
                return false;
        }

        return true;
    }
}
