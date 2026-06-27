using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

public class Vic2SpriteManagerTests
{
    private static readonly string RasterCompareIrqSource = Vic2IRQ.GetInterruptSourceName(IRQSource.RasterCompare);
    private static readonly string SpriteToSpriteCollisionIrqSource = Vic2IRQ.GetInterruptSourceName(IRQSource.SpriteToSpriteCollision);

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

    [Fact]
    public void Sprite_to_sprite_collision_raises_collision_irq_and_not_raster_irq()
    {
        var c64 = BuildC64();

        // A game using raster splits has the raster IRQ enabled; enable the sprite-to-sprite
        // collision IRQ too ($D01A bit 0 = raster-compare, bit 1 = sprite-to-sprite collision).
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0b0000_0011);

        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 10, y: 10, spritePointer: 192);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 20, y: 15, spritePointer: 193);

        c64.Vic2.SpriteManager.SetCollitionDetectionStatesAndIRQ();

        // A collision must raise its own collision IRQ source ($D019 bit 1)...
        Assert.True(c64.CPU.CPUInterrupts.IsIRQSourceActive(SpriteToSpriteCollisionIrqSource));
        // ...and must never raise the raster-compare IRQ source ($D019 bit 0).
        Assert.False(c64.CPU.CPUInterrupts.IsIRQSourceActive(RasterCompareIrqSource));
    }

    [Fact]
    public void Sprite_collision_does_not_raise_raster_irq_when_only_raster_irq_enabled()
    {
        // Regression for the Great Giana Sisters status-panel jitter: the game enables the
        // raster IRQ (for screen splits) but not the collision IRQ. A sprite collision must
        // not raise a spurious raster-compare IRQ that the game would service as an extra split.
        var c64 = BuildC64();
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0b0000_0001); // enable raster-compare only

        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 10, y: 10, spritePointer: 192);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 20, y: 15, spritePointer: 193);

        c64.Vic2.SpriteManager.SetCollitionDetectionStatesAndIRQ();

        Assert.False(c64.CPU.CPUInterrupts.IsIRQSourceActive(RasterCompareIrqSource));
    }

    [Fact]
    public void PerLine_sprite_to_sprite_collision_matches_end_of_frame_for_static_overlap()
    {
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 10, y: 10, spritePointer: 192);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 20, y: 15, spritePointer: 193);

        DrivePerLineCollisionsForFrame(c64);

        // Same result as the end-of-frame path for a static scene (see the non-per-line test above).
        Assert.Equal(0b0000_0011, c64.Vic2.SpriteManager.SpriteToSpriteCollisionStore);
    }

    [Fact]
    public void PerLine_sprite_to_sprite_no_collision_for_non_overlapping()
    {
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 10, y: 10, spritePointer: 192);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 80, y: 80, spritePointer: 193);

        DrivePerLineCollisionsForFrame(c64);

        Assert.Equal(0, c64.Vic2.SpriteManager.SpriteToSpriteCollisionStore);
    }

    [Fact]
    public void PerLine_collision_detected_for_earlier_band_even_when_final_position_does_not_overlap()
    {
        // The multiplex case the end-of-frame path cannot see: a hardware sprite collides during an
        // early band, then is repositioned away. The end-of-frame single-position check (final Y)
        // misses it; the per-line accumulation catches the band where it actually overlapped.
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 30, y: 10, spritePointer: 193); // static, top
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 30, y: 10, spritePointer: 192); // band 1 overlaps sprite 1

        var sm = c64.Vic2.SpriteManager;
        var totalHeight = c64.Vic2.Vic2Model.TotalHeight;
        for (int line = 0; line < totalHeight; line++)
        {
            // After sprite 0's first band (raster 10..30) finishes, move it far away (multiplex reuse).
            if (line == 40)
                c64.WriteIOStorage(Vic2Addr.SPRITE_0_Y, 200);
            sm.AccumulatePerLineCollisions(line);
        }

        // Per-line caught the band-1 overlap.
        Assert.Equal(0b0000_0011, sm.SpriteToSpriteCollisionStore);
        // ...and the end-of-frame single-position check (sprite 0 now at y=200) would have missed it.
        Assert.Equal(0, sm.GetSpriteToSpriteCollision());
    }

    [Fact]
    public void PerLine_sprite_collision_raises_collision_irq_and_not_raster_irq()
    {
        var c64 = BuildC64(perLineSprites: true);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0b0000_0011);

        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 10, y: 10, spritePointer: 192);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 20, y: 15, spritePointer: 193);

        DrivePerLineCollisionsForFrame(c64);
        c64.Vic2.SpriteManager.SetCollitionDetectionStatesAndIRQ();

        Assert.True(c64.CPU.CPUInterrupts.IsIRQSourceActive(SpriteToSpriteCollisionIrqSource));
        Assert.False(c64.CPU.CPUInterrupts.IsIRQSourceActive(RasterCompareIrqSource));
    }

    private static void DrivePerLineCollisionsForFrame(C64 c64)
    {
        var sm = c64.Vic2.SpriteManager;
        var totalHeight = c64.Vic2.Vic2Model.TotalHeight;
        for (int line = 0; line < totalHeight; line++)
            sm.AccumulatePerLineCollisions(line);
    }

    private static C64 BuildC64(bool perLineSprites = false)
    {
        return C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
            // These tests target the end-of-frame collision recompute path; pin the mode so the
            // (default-on) per-line collision path doesn't skip it. Per-line is covered separately.
            Vic2RasterizerPerLineSprites = perLineSprites,
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
