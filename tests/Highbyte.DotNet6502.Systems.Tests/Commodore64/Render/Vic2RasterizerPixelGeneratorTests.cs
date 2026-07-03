using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Render;

public class Vic2RasterizerPixelGeneratorTests
{
    [Fact]
    public void DrawSprites_renders_double_width_sprite_at_double_horizontal_span()
    {
        var c64 = BuildC64();
        CreateVisibleSprite(c64, spriteNumber: 0, doubleWidth: true, doubleHeight: false, CreateSingleRowSprite(0b1111_0000), spritePointer: 192);
        var foreground = RenderSprites(c64);

        var (_, startX, endX) = GetFirstRenderedSpan(foreground, c64.Screen.VisibleWidth);

        Assert.Equal(8, endX - startX + 1);
    }

    [Fact]
    public void DrawSprites_does_not_expand_width_for_double_height_only_sprite()
    {
        var c64 = BuildC64();
        CreateVisibleSprite(c64, spriteNumber: 0, doubleWidth: false, doubleHeight: true, CreateSingleRowSprite(0b1111_0000), spritePointer: 192);
        var foreground = RenderSprites(c64);

        var (_, startX, endX) = GetFirstRenderedSpan(foreground, c64.Screen.VisibleWidth);

        Assert.Equal(4, endX - startX + 1);
    }

    [Fact]
    public void DrawSprites_preserves_y_position_after_empty_leading_rows()
    {
        var c64 = BuildC64();
        CreateVisibleSprite(c64, spriteNumber: 0, doubleWidth: false, doubleHeight: false, CreateSingleRowSprite(0b1111_0000, rowIndex: 2), spritePointer: 192);
        var foreground = RenderSprites(c64);
        var visibleMainScreenArea = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        var (row, _, _) = GetFirstRenderedSpan(foreground, c64.Screen.VisibleWidth);

        Assert.Equal(visibleMainScreenArea.Screen.Start.Y + 2, row);
    }

    [Fact]
    public void DrawSprites_clips_right_edge_to_38_column_border()
    {
        var c64 = BuildC64();
        var normalLayout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        var col38Layout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: true);
        var spriteScreenX = col38Layout.RightBorder.Start.X - 4;
        var spriteX = c64.Vic2.SpriteManager.ScreenOffsetX + spriteScreenX - normalLayout.Screen.Start.X;
        SetAllScreenLinesToColumnMode(c64, colMode40: false);
        CreateVisibleSprite(c64, spriteNumber: 0, doubleWidth: false, doubleHeight: false, CreateSingleRowSprite(0xff), spritePointer: 192, x: spriteX);

        var foreground = RenderSprites(c64);

        var (_, startX, endX) = GetFirstRenderedSpan(foreground, c64.Screen.VisibleWidth);
        Assert.Equal(spriteScreenX, startX);
        Assert.Equal(col38Layout.RightBorder.Start.X - 1, endX);
    }

    [Theory]
    [InlineData("C64PAL", "PAL")]
    [InlineData("C64NTSC", "NTSC")]
    public void ConvertRasterLineToScreenLine_aligns_first_display_raster_line_with_visible_layout(string c64Model, string vic2Model)
    {
        var c64 = BuildC64(c64Model, vic2Model);
        var visibleMainScreenArea = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.Visible, for24RowMode: false, for38ColMode: false);

        var screenLine = c64.Vic2.Vic2Model.ConvertRasterLineToScreenLine(c64.Vic2.Vic2Model.FirstRasterLineOfMainScreen);

        Assert.Equal(visibleMainScreenArea.Screen.Start.Y, screenLine);
    }

    [Fact]
    public void Vic2_register_mirrors_update_display_state_used_by_raster_timed_cartridge_code()
    {
        var c64 = BuildC64();

        c64.Mem.Write(0xD051, 0x3B); // Mirror of $D011.
        c64.Mem.Write(0xD058, 0xCD); // Mirror of $D018.

        Assert.Equal(Vic2.DispMode.Bitmap, c64.Vic2.DisplayMode);
        Assert.Equal(0x3000, c64.Vic2.VideoMatrixBaseAddress);
        Assert.Equal(0x2000, c64.Vic2.BitmapManager.BitmapAddressInVIC2Bank);
    }

    private static C64 BuildC64()
    {
        return BuildC64("C64PAL", "PAL");
    }

    private static C64 BuildC64(string c64Model, string vic2Model)
    {
        return C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = c64Model,
            Vic2Model = vic2Model
        }, NullLoggerFactory.Instance);
    }

    private static void CreateVisibleSprite(C64 c64, int spriteNumber, bool doubleWidth, bool doubleHeight, byte[] spriteShape, byte spritePointer, int? x = null)
    {
        var spriteManager = c64.Vic2.SpriteManager;
        var spriteX = x ?? spriteManager.ScreenOffsetX;
        c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_X + spriteNumber * 2), (byte)(spriteX & 0xff));
        c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_Y + spriteNumber * 2), (byte)spriteManager.ScreenOffsetY);
        var spriteMsbX = c64.ReadIOStorage(Vic2Addr.SPRITE_MSB_X);
        spriteMsbX = spriteX > 255 ? (byte)(spriteMsbX | (1 << spriteNumber)) : (byte)(spriteMsbX & ~(1 << spriteNumber));
        c64.WriteIOStorage(Vic2Addr.SPRITE_MSB_X, spriteMsbX);

        var spriteEnable = c64.ReadIOStorage(Vic2Addr.SPRITE_ENABLE);
        spriteEnable |= (byte)(1 << spriteNumber);
        c64.WriteIOStorage(Vic2Addr.SPRITE_ENABLE, spriteEnable);

        var spriteXExpand = c64.ReadIOStorage(Vic2Addr.SPRITE_X_EXPAND);
        spriteXExpand = doubleWidth ? (byte)(spriteXExpand | (1 << spriteNumber)) : (byte)(spriteXExpand & ~(1 << spriteNumber));
        c64.WriteIOStorage(Vic2Addr.SPRITE_X_EXPAND, spriteXExpand);

        var spriteYExpand = c64.ReadIOStorage(Vic2Addr.SPRITE_Y_EXPAND);
        spriteYExpand = doubleHeight ? (byte)(spriteYExpand | (1 << spriteNumber)) : (byte)(spriteYExpand & ~(1 << spriteNumber));
        c64.WriteIOStorage(Vic2Addr.SPRITE_Y_EXPAND, spriteYExpand);

        c64.Vic2.Vic2Mem[(ushort)(spriteManager.SpritePointerStartAddress + spriteNumber)] = spritePointer;
        var spriteDataAddress = (ushort)(spritePointer * 64);
        for (int i = 0; i < spriteShape.Length; i++)
        {
            c64.Vic2.Vic2Mem[(ushort)(spriteDataAddress + i)] = spriteShape[i];
        }
    }

    private static uint[] RenderSprites(C64 c64)
    {
        var pixelCount = c64.Screen.VisibleWidth * c64.Screen.VisibleHeight;
        var background = new uint[pixelCount];
        var foreground = new uint[pixelCount];

        var generator = new Vic2RasterizerUintPixelGenerator(
            c64,
            (packedBgra, index, toForeground) =>
            {
                if (toForeground)
                    foreground[index] = packedBgra;
                else
                    background[index] = packedBgra;
            },
            (source, sourceIndex, destIndex, width) => source.Slice(sourceIndex, width).CopyTo(background.AsSpan(destIndex, width)),
            (destIndex, width) => background.AsSpan(destIndex, width).Clear(),
            (source, sourceIndex, destIndex, width) => source.Slice(sourceIndex, width).CopyTo(foreground.AsSpan(destIndex, width)),
            (destIndex, width) => foreground.AsSpan(destIndex, width).Clear());

        generator.DrawSpritesToBitmapBackedByPixelArray();
        return foreground;
    }

    private static void SetAllScreenLinesToColumnMode(C64 c64, bool colMode40)
    {
        foreach (var screenLineData in c64.Vic2.ScreenLineIORegisterValues.Values)
        {
            screenLineData.ColMode40 = colMode40;
        }
    }

    private static (int Row, int StartX, int EndX) GetFirstRenderedSpan(uint[] pixels, int width)
    {
        var height = pixels.Length / width;
        for (var row = 0; row < height; row++)
        {
            var rowStart = row * width;
            var startX = -1;
            var endX = -1;
            for (var x = 0; x < width; x++)
            {
                if (pixels[rowStart + x] == 0)
                    continue;

                startX = x;
                break;
            }

            if (startX < 0)
                continue;

            for (var x = width - 1; x >= startX; x--)
            {
                if (pixels[rowStart + x] == 0)
                    continue;

                endX = x;
                break;
            }

            return (row, startX, endX);
        }

        throw new Xunit.Sdk.XunitException("Expected rendered sprite pixels, but no non-zero pixels were found.");
    }

    private static byte[] CreateSingleRowSprite(byte firstRowFirstByte, int rowIndex = 0)
    {
        var spriteShape = new byte[63];
        spriteShape[rowIndex * 3] = firstRowFirstByte;
        return spriteShape;
    }
}
