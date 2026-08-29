using Highbyte.DotNet6502.Systems.Oric.Render;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricRasterizerTests
{
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
