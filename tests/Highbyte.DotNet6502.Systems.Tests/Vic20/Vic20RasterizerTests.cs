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
    public void Vic20DefaultsMatchUnexpandedMemoryMap()
    {
        var config = new Vic20Config();
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(config, NullLoggerFactory.Instance);

        Assert.Equal(0x1E00, config.ScreenStartAddress);
        Assert.Equal(0x9600, config.ColorStartAddress);
        Assert.Equal(0x1E00, vic20.CurrentVideoLayout.ScreenStartAddress);
        Assert.Equal(0x9600, vic20.CurrentVideoLayout.ColorStartAddress);
        Assert.Equal(Highbyte.DotNet6502.Systems.Vic20.Vic20.BASIC_LOAD_ADDRESS, (ushort)0x1001);
    }

    [Fact]
    public void Vic20UsesOnlyUnexpandedRamRegionsByDefault()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);

        vic20.Mem[0x0002] = 0x12;
        vic20.Mem[0x1001] = 0x34;
        vic20.Mem[0x0400] = 0x56;
        vic20.Mem[0x2000] = 0x78;

        Assert.Equal(0x12, vic20.Mem[0x0002]);
        Assert.Equal(0x34, vic20.Mem[0x1001]);
        Assert.Equal(0xFF, vic20.Mem[0x0400]);
        Assert.Equal(0xFF, vic20.Mem[0x2000]);
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

        var cellWidth = 8 * Vic20Config.PixelScaleX;
        var borderX = (rasterizer.NativeSize.Width - layout.Columns * cellWidth) / 2;
        var borderY = (rasterizer.NativeSize.Height - layout.Rows * 8) / 2;
        var foreground = rasterizer.CurrentFrontLayerBuffers[1].Span;
        var foregroundArgb = Pack(ColorMaps.Vic20ColorMap[0x06]);
        // Bit 7 (MSB) of the glyph maps to source pixel 0, which spans buffer pixels [borderX, borderX+1] with 2x stretch.
        for (var x = 0; x < Vic20Config.PixelScaleX; x++)
            Assert.Equal(foregroundArgb, foreground[borderY * rasterizer.NativeSize.Width + borderX + x]);
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
        // Multicolor pairs (high-to-low): 00=bg, 01=border, 10=fg, 11=aux
        vic20.Mem[0x1408] = 0b0001_1011;

        var rasterizer = new Vic20Rasterizer(vic20);
        rasterizer.OnEndFrame();

        var cellWidth = 8 * Vic20Config.PixelScaleX;
        var borderX = (rasterizer.NativeSize.Width - layout.Columns * cellWidth) / 2;
        var borderY = (rasterizer.NativeSize.Height - layout.Rows * 8) / 2;
        var foreground = rasterizer.CurrentFrontLayerBuffers[1].Span;
        var rowOffset = borderY * rasterizer.NativeSize.Width;

        // Each pair covers 2 source pixels × PixelScaleX = 4 buffer pixels.
        var pairBufferWidth = 2 * Vic20Config.PixelScaleX;
        // Pair 1 (bits 5-4 = 01) → border color (0x03)
        for (var x = 0; x < pairBufferWidth; x++)
            Assert.Equal(Pack(ColorMaps.Vic20ColorMap[0x03]), foreground[rowOffset + borderX + pairBufferWidth + x]);
        // Pair 2 (bits 3-2 = 10) → foreground color (0x02)
        for (var x = 0; x < pairBufferWidth; x++)
            Assert.Equal(Pack(ColorMaps.Vic20ColorMap[0x02]), foreground[rowOffset + borderX + 2 * pairBufferWidth + x]);
        // Pair 3 (bits 1-0 = 11) → auxiliary color (0x0B)
        for (var x = 0; x < pairBufferWidth; x++)
            Assert.Equal(Pack(ColorMaps.Vic20ColorMap[0x0B]), foreground[rowOffset + borderX + 3 * pairBufferWidth + x]);
    }

    [Fact]
    public void RasterizerCentersCharacterAreaWhenColumnsIncrease()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
        vic20.Mem[Vic20VideoLayout.RegisterColumns] = Vic20VideoLayout.EncodeColumnsRegister(0x1000, 24);
        vic20.Mem[Vic20VideoLayout.RegisterAddress] = Vic20VideoLayout.EncodeAddressRegister(0x1000, 0x1400);

        var layout = vic20.CurrentVideoLayout;
        Assert.Equal(24, layout.Columns);

        vic20.Mem[layout.ScreenStartAddress] = 0x01;
        vic20.Mem[layout.ColorStartAddress] = 0x06;
        vic20.Mem[0x1408] = 0b1000_0000;

        var rasterizer = new Vic20Rasterizer(vic20);
        rasterizer.OnEndFrame();

        var cellWidth = 8 * Vic20Config.PixelScaleX;
        var borderX = (rasterizer.NativeSize.Width - layout.Columns * cellWidth) / 2;
        Assert.True(borderX >= 0);

        var foreground = rasterizer.CurrentFrontLayerBuffers[1].Span;
        var foregroundArgb = Pack(ColorMaps.Vic20ColorMap[0x06]);
        var borderY = (rasterizer.NativeSize.Height - layout.Rows * 8) / 2;
        Assert.Equal(foregroundArgb, foreground[borderY * rasterizer.NativeSize.Width + borderX]);
    }

    [Fact]
    public void RasterizerCentersCharacterAreaWhenColumnsDecrease()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
        vic20.Mem[Vic20VideoLayout.RegisterColumns] = Vic20VideoLayout.EncodeColumnsRegister(0x1000, 16);
        vic20.Mem[Vic20VideoLayout.RegisterAddress] = Vic20VideoLayout.EncodeAddressRegister(0x1000, 0x1400);

        var layout = vic20.CurrentVideoLayout;
        Assert.Equal(16, layout.Columns);

        vic20.Mem[layout.ScreenStartAddress] = 0x01;
        vic20.Mem[layout.ColorStartAddress] = 0x06;
        vic20.Mem[0x1408] = 0b1000_0000;

        var rasterizer = new Vic20Rasterizer(vic20);
        rasterizer.OnEndFrame();

        var cellWidth = 8 * Vic20Config.PixelScaleX;
        var borderX = (rasterizer.NativeSize.Width - layout.Columns * cellWidth) / 2;
        Assert.True(borderX > 0);

        var foreground = rasterizer.CurrentFrontLayerBuffers[1].Span;
        var foregroundArgb = Pack(ColorMaps.Vic20ColorMap[0x06]);
        var borderY = (rasterizer.NativeSize.Height - layout.Rows * 8) / 2;
        Assert.Equal(foregroundArgb, foreground[borderY * rasterizer.NativeSize.Width + borderX]);
    }

    [Fact]
    public void RasterizerClampsWhenColumnsExceedBufferWidth()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
        vic20.Mem[Vic20VideoLayout.RegisterColumns] = Vic20VideoLayout.EncodeColumnsRegister(0x1000, 40);
        vic20.Mem[Vic20VideoLayout.RegisterAddress] = Vic20VideoLayout.EncodeAddressRegister(0x1000, 0x1400);

        var layout = vic20.CurrentVideoLayout;
        Assert.Equal(40, layout.Columns);

        var rasterizer = new Vic20Rasterizer(vic20);
        // Should not throw — columns are clamped to buffer width
        rasterizer.OnEndFrame();
    }

    [Fact]
    public void Vic20UsesNtscTvModelByDefault()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
        Assert.Equal(TvModel.Ntsc.MaxVisibleWidth, vic20.VisibleWidth);
        Assert.Equal(TvModel.Ntsc.MaxVisibleHeight, vic20.VisibleHeight);
        Assert.Equal(TvModel.Ntsc.RefreshFrequencyHz, vic20.Screen.RefreshFrequencyHz);
    }

    [Fact]
    public void Vic20HonorsPalTvModelWhenConfigured()
    {
        var config = new Vic20Config { TvModel = TvModel.Pal };
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(config, NullLoggerFactory.Instance);
        Assert.Equal(TvModel.Pal.MaxVisibleWidth, vic20.VisibleWidth);
        Assert.Equal(TvModel.Pal.MaxVisibleHeight, vic20.VisibleHeight);
        Assert.Equal(TvModel.Pal.RefreshFrequencyHz, vic20.Screen.RefreshFrequencyHz);
    }

    private static uint Pack(System.Drawing.Color color)
        => Vic20Rasterizer.PackBgra(color.B, color.G, color.R, color.A);
}
