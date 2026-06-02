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

    private static C64 BuildC64()
    {
        return C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL"
        }, NullLoggerFactory.Instance);
    }

    private static void CreateVisibleSprite(C64 c64, int spriteNumber, bool doubleWidth, bool doubleHeight, byte[] spriteShape, byte spritePointer)
    {
        var spriteManager = c64.Vic2.SpriteManager;
        c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_X + spriteNumber * 2), (byte)spriteManager.ScreenOffsetX);
        c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_Y + spriteNumber * 2), (byte)spriteManager.ScreenOffsetY);

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

    private static byte[] CreateSingleRowSprite(byte firstRowFirstByte)
    {
        var spriteShape = new byte[63];
        spriteShape[0] = firstRowFirstByte;
        return spriteShape;
    }
}
