using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Render;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricRasterizerTests
{
    [Fact]
    public void ProgressiveRasterPreservesTemporaryPaperChangeOnScannedLine()
    {
        var oric = new OricMachine();
        var rasterizer = Assert.IsType<OricRasterizer>(oric.RenderProvider);
        oric.Mem[OricRasterizer.TextScreenAddress] = 0x16; // cyan paper
        oric.Mem[(ushort)(OricRasterizer.TextScreenAddress + 1)] = (byte)' ';

        oric.RasterClock.Advance(OricConfig.VisibleRasterStartLine * OricConfig.CyclesPerLine);
        oric.Mem[OricRasterizer.TextScreenAddress] = 0x14; // restore blue after line zero was scanned
        oric.RasterClock.Advance(OricConfig.CyclesPerLine);
        oric.RasterClock.Advance(
            (OricConfig.LinesPerFrame - OricConfig.VisibleRasterStartLine - 1) * OricConfig.CyclesPerLine);

        var pixels = rasterizer.CurrentFrontBuffer.Span;
        Assert.Equal(OricRasterizer.PackBgra(0xff, 0xff, 0x00, 0xff), pixels[OricConfig.CharacterWidth]);
        Assert.Equal(
            OricRasterizer.PackBgra(0xff, 0x00, 0x00, 0xff),
            pixels[OricConfig.VisibleWidth + OricConfig.CharacterWidth]);
    }

    [Fact]
    public void TextGlyphUsesSixPixelsAndRealRamCharset()
    {
        var oric = new OricMachine();
        var rasterizer = Assert.IsType<OricRasterizer>(oric.RenderProvider);
        oric.Mem[OricRasterizer.TextScreenAddress] = (byte)'A';
        oric.Mem[(ushort)(OricRasterizer.TextStandardCharsetAddress + 'A' * 8)] = 0x20;

        rasterizer.OnEndFrame();
        var pixels = ((IVideoFrameProvider)rasterizer).CurrentFrontBuffer.Span;

        Assert.Equal(OricRasterizer.PackBgra(0xff, 0xff, 0xff, 0xff), pixels[0]);
        Assert.Equal(OricRasterizer.PackBgra(0x00, 0x00, 0x00, 0xff), pixels[1]);
    }

    [Fact]
    public void SerialInkAttributeChangesFollowingPixels()
    {
        var oric = new OricMachine();
        var rasterizer = Assert.IsType<OricRasterizer>(oric.RenderProvider);
        oric.Mem[OricRasterizer.TextScreenAddress] = 0x01; // red ink
        oric.Mem[(ushort)(OricRasterizer.TextScreenAddress + 1)] = (byte)'A';
        oric.Mem[(ushort)(OricRasterizer.TextStandardCharsetAddress + 'A' * 8)] = 0x20;

        rasterizer.OnEndFrame();
        var pixels = ((IVideoFrameProvider)rasterizer).CurrentFrontBuffer.Span;

        Assert.Equal(OricRasterizer.PackBgra(0x00, 0x00, 0xff, 0xff), pixels[6]);
    }
}
