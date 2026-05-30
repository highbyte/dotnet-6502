using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20.Render;
using Highbyte.DotNet6502.Systems.Vic20.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Vic20;

public class Vic20RasterizerTests
{
    [Fact]
    public void VideoLayoutDecodesAddressesAndGeometryFromVicRegisters()
    {
        var mem = new Memory();
        mem[Vic20VideoLayout.RegisterColumns] = 0x96;
        mem[Vic20VideoLayout.RegisterRows] = 0x2E;
        mem[Vic20VideoLayout.RegisterAddress] = 0xF0;
        mem[Vic20VideoLayout.RegisterAuxiliaryColor] = 0xA0;
        mem[Vic20VideoLayout.RegisterBackgroundBorderColor] = 0x1B;

        var layout = Vic20VideoLayout.FromMemory(mem, new Vic20Config());

        Assert.Equal(0x1E00, layout.ScreenStartAddress);
        Assert.Equal(0x9600, layout.ColorStartAddress);
        Assert.Equal(0x8000, layout.CharacterStartAddress);
        Assert.Equal(22, layout.Columns);
        Assert.Equal(23, layout.Rows);
        Assert.Equal(8, layout.CharacterHeight);
        Assert.Equal(0x01, layout.BackgroundColor);
        Assert.Equal(0x03, layout.BorderColor);
        Assert.Equal(0x0A, layout.AuxiliaryColor);
        Assert.False(layout.ReverseScreen);
    }

    [Fact]
    public void RasterizerReadsGlyphPixelsFromTheActiveCharacterBase()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
        vic20.Mem[Vic20VideoLayout.RegisterAddress] = Vic20VideoLayout.EncodeAddressRegister(0x1000, 0x1400);

        var layout = vic20.CurrentVideoLayout;
        vic20.Mem[layout.ScreenStartAddress] = 0x01;
        vic20.Mem[layout.ColorStartAddress] = 0x06;

        vic20.Mem[0x1408] = 0b1000_0000;
        var rasterizer = new Vic20Rasterizer(vic20);
        rasterizer.OnEndFrame();

        var foreground = rasterizer.CurrentFrontLayerBuffers[1].Span;
        var foregroundArgb = Pack(Highbyte.DotNet6502.Systems.Vic20.Video.ColorMaps.Vic20ColorMap[0x06]);
        Assert.Equal(foregroundArgb, foreground[(8 * rasterizer.NativeSize.Width) + 40]);
    }

    [Fact]
    public void RasterizerUsesVic20PerCellMulticolorRules()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
        vic20.Mem[Vic20VideoLayout.RegisterAddress] = Vic20VideoLayout.EncodeAddressRegister(0x1000, 0x1400);
        vic20.Mem[Vic20VideoLayout.RegisterAuxiliaryColor] = 0xB0;

        var layout = vic20.CurrentVideoLayout;
        vic20.Mem[layout.ScreenStartAddress] = 0x01;
        vic20.Mem[layout.ColorStartAddress] = 0x0A;
        vic20.Mem[0x1408] = 0b0001_1011;

        var rasterizer = new Vic20Rasterizer(vic20);
        rasterizer.OnEndFrame();

        var foreground = rasterizer.CurrentFrontLayerBuffers[1].Span;
        var rowOffset = 8 * rasterizer.NativeSize.Width;

        Assert.Equal(Pack(Highbyte.DotNet6502.Systems.Vic20.Video.ColorMaps.Vic20ColorMap[0x03]), foreground[rowOffset + 42]);
        Assert.Equal(Pack(Highbyte.DotNet6502.Systems.Vic20.Video.ColorMaps.Vic20ColorMap[0x03]), foreground[rowOffset + 43]);
        Assert.Equal(Pack(Highbyte.DotNet6502.Systems.Vic20.Video.ColorMaps.Vic20ColorMap[0x02]), foreground[rowOffset + 44]);
        Assert.Equal(Pack(Highbyte.DotNet6502.Systems.Vic20.Video.ColorMaps.Vic20ColorMap[0x02]), foreground[rowOffset + 45]);
        Assert.Equal(Pack(Highbyte.DotNet6502.Systems.Vic20.Video.ColorMaps.Vic20ColorMap[0x0B]), foreground[rowOffset + 46]);
        Assert.Equal(Pack(Highbyte.DotNet6502.Systems.Vic20.Video.ColorMaps.Vic20ColorMap[0x0B]), foreground[rowOffset + 47]);
    }

    private static uint Pack(System.Drawing.Color color)
        => Vic20Rasterizer.PackBgra(color.B, color.G, color.R, color.A);
}
