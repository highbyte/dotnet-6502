using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

public class Vic2SpriteManagerTests
{
    [Fact]
    public void GetSpriteToSpriteCollision_returns_zero_for_non_overlapping_visible_sprites()
    {
        var c64 = BuildC64();
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 10, y: 10, spritePointer: 192);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 80, y: 80, spritePointer: 193);

        var collision = c64.Vic2.SpriteManager.GetSpriteToSpriteCollision();

        Assert.Equal(0, collision);
    }

    [Fact]
    public void GetSpriteToSpriteCollision_sets_bits_for_overlapping_visible_sprites()
    {
        var c64 = BuildC64();
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 10, y: 10, spritePointer: 192);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 20, y: 15, spritePointer: 193);

        var collision = c64.Vic2.SpriteManager.GetSpriteToSpriteCollision();

        Assert.Equal(0b0000_0011, collision);
    }

    [Fact]
    public void GetSpriteToSpriteCollision_still_detects_overlap_after_empty_leading_rows()
    {
        var c64 = BuildC64();
        CreateVisibleSprite(c64, spriteNumber: 0, x: 10, y: 10, spritePointer: 192, CreateSingleRowSprite(rowIndex: 1, firstRowFirstByte: 0xF0));
        CreateVisibleSprite(c64, spriteNumber: 1, x: 10, y: 10, spritePointer: 193, CreateSingleRowSprite(rowIndex: 1, firstRowFirstByte: 0xF0));

        var collision = c64.Vic2.SpriteManager.GetSpriteToSpriteCollision();

        Assert.Equal(0b0000_0011, collision);
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

    private static void CreateVisibleSolidSprite(C64 c64, int spriteNumber, byte x, byte y, byte spritePointer)
    {
        CreateVisibleSprite(c64, spriteNumber, x, y, spritePointer, Enumerable.Repeat((byte)0xFF, 63).ToArray());
    }

    private static void CreateVisibleSprite(C64 c64, int spriteNumber, byte x, byte y, byte spritePointer, byte[] spriteShape)
    {
        c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_X + spriteNumber * 2), x);
        c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_Y + spriteNumber * 2), y);

        var spriteEnable = c64.ReadIOStorage(Vic2Addr.SPRITE_ENABLE);
        spriteEnable |= (byte)(1 << spriteNumber);
        c64.WriteIOStorage(Vic2Addr.SPRITE_ENABLE, spriteEnable);

        var spriteManager = c64.Vic2.SpriteManager;
        c64.Vic2.Vic2Mem[(ushort)(spriteManager.SpritePointerStartAddress + spriteNumber)] = spritePointer;

        var spriteDataAddress = (ushort)(spritePointer * 64);
        for (ushort i = 0; i < spriteShape.Length; i++)
        {
            c64.Vic2.Vic2Mem[(ushort)(spriteDataAddress + i)] = spriteShape[i];
        }
    }

    private static byte[] CreateSingleRowSprite(int rowIndex, byte firstRowFirstByte)
    {
        var spriteShape = new byte[63];
        spriteShape[rowIndex * 3] = firstRowFirstByte;
        return spriteShape;
    }
}
