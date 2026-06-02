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
        c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_X + spriteNumber * 2), x);
        c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_Y + spriteNumber * 2), y);

        var spriteEnable = c64.ReadIOStorage(Vic2Addr.SPRITE_ENABLE);
        spriteEnable |= (byte)(1 << spriteNumber);
        c64.WriteIOStorage(Vic2Addr.SPRITE_ENABLE, spriteEnable);

        var spriteManager = c64.Vic2.SpriteManager;
        c64.Vic2.Vic2Mem[(ushort)(spriteManager.SpritePointerStartAddress + spriteNumber)] = spritePointer;

        var spriteDataAddress = (ushort)(spritePointer * 64);
        for (ushort i = 0; i < 63; i++)
        {
            c64.Vic2.Vic2Mem[(ushort)(spriteDataAddress + i)] = 0xFF;
        }
    }
}
