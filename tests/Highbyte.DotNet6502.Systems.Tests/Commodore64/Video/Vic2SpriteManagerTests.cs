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
        // collision IRQ too ($D01A bit 0 = raster-compare, bit 2 = sprite-to-sprite collision).
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0b0000_0101);   // raster compare and sprite-to-sprite collision

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

        var totalHeight = c64.Vic2.Vic2Model.TotalHeight;
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        for (int line = 0; line <= totalHeight; line++)
        {
            // After sprite 0's first band (raster 11..31) finishes, move it far away (multiplex reuse).
            if (line == 40)
                c64.WriteIOStorage(Vic2Addr.SPRITE_0_Y, 200);
            c64.Vic2.AdvanceRaster(cyclesPerLine);
        }

        // Per-line caught the band-1 overlap.
        Assert.Equal(0b0000_0011, c64.Vic2.SpriteManager.SpriteToSpriteCollisionStore);
        // ...and the end-of-frame single-position check (sprite 0 now at y=200) would have missed it.
        Assert.Equal(0, c64.Vic2.SpriteManager.GetSpriteToSpriteCollision());
    }

    [Fact]
    public void PerLine_sprite_collision_raises_collision_irq_and_not_raster_irq()
    {
        var c64 = BuildC64(perLineSprites: true);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0b0000_0101);   // raster compare and sprite-to-sprite collision

        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 10, y: 10, spritePointer: 192);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 20, y: 15, spritePointer: 193);

        DrivePerLineCollisionsForFrame(c64);
        c64.Vic2.SpriteManager.SetCollitionDetectionStatesAndIRQ();

        Assert.True(c64.CPU.CPUInterrupts.IsIRQSourceActive(SpriteToSpriteCollisionIrqSource));
        Assert.False(c64.CPU.CPUInterrupts.IsIRQSourceActive(RasterCompareIrqSource));
    }

    [Fact]
    public void PerLine_sprite_collision_raises_collision_irq_mid_frame()
    {
        // The collision IRQ should be raised at the raster line where the collision occurs, not only
        // at end-of-frame. Drive the raster lines directly and assert the IRQ becomes active WITHOUT
        // calling SetCollitionDetectionStatesAndIRQ.
        var c64 = BuildC64(perLineSprites: true);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0b0000_0100); // enable sprite-to-sprite collision IRQ only (bit 2, IMMC)

        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 20, y: 60, spritePointer: 192);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 25, y: 60, spritePointer: 193);

        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;

        // Before the sprites' display band (raster 61..81): no collision, no IRQ.
        c64.Vic2.AdvanceRaster(cyclesPerLine * 51);   // into line 51
        Assert.False(c64.CPU.CPUInterrupts.IsIRQSourceActive(SpriteToSpriteCollisionIrqSource));

        // Into the band: the collision is latched as the band's first line ends, and must raise the
        // IRQ there, mid-frame.
        c64.Vic2.AdvanceRaster(cyclesPerLine * 12);   // into line 63
        Assert.True(c64.CPU.CPUInterrupts.IsIRQSourceActive(SpriteToSpriteCollisionIrqSource));
    }

    // Drives the raster through a frame line by line: the VIC-II's own per-line work captures the
    // sprite snapshot, accumulates the per-line collisions and, as each line ends, derives the
    // sprites' output runs and their sprite-to-sprite collisions.
    private static void DrivePerLineCollisionsForFrame(C64 c64)
    {
        var totalHeight = c64.Vic2.Vic2Model.TotalHeight;
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        for (int line = 0; line <= totalHeight; line++)
            c64.Vic2.AdvanceRaster(cyclesPerLine);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sprites_beyond_x_255_with_sparse_rows_collide(bool perLineSprites)
    {
        // VICE's spritex testsuite, its first case, held still: sprite 0 solid and sprite 7 with only
        // its row 6's first and last pixel, both at X 72 with the X high bit set (328) and Y 49.
        // Sprite 7's two pixels lie inside sprite 0's row 6, so the two collide.
        var c64 = BuildC64(perLineSprites);
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 72, y: 49, spritePointer: 0xF8);
        CreateVisibleSprite(c64, spriteNumber: 7, x: 72, y: 49, spritePointer: 0xFC, SparseRow6());
        c64.WriteIOStorage(Vic2Addr.SPRITE_MSB_X, 0x81);

        if (perLineSprites)
            DrivePerLineCollisionsForFrame(c64);
        else
            c64.Vic2.SpriteManager.SetCollitionDetectionStatesAndIRQ();

        Assert.Equal(0x81, c64.Vic2.SpriteManager.SpriteToSpriteCollisionStore);
    }

    [Theory]
    [InlineData(24, 0)]      // written in the cycle whose fourth pixel is X 95: compared from pixel 4, so the old X matches
    [InlineData(23, 0x81)]   // written a cycle earlier: the new X 96 matches, and its last pixel meets sprite 7's first
    public void Sprite_X_written_in_a_cycle_is_compared_from_its_fifth_pixel(int writeCycle, byte expectedCollision)
    {
        // VICE's spritex suite, tests 2 and 3 on the 6569: sprite 0 solid at X 95, sprite 7 with only
        // its row 6's first and last pixel at X 119, sprite 0's X written to 96 on row 6's line.
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 95, y: 49, spritePointer: 0xF8);
        CreateVisibleSprite(c64, spriteNumber: 7, x: 119, y: 49, spritePointer: 0xFC, SparseRow6());

        RunFrameWithWriteAt(c64, line: 49 + 1 + 6, writeCycle, () => c64.Mem.Write(Vic2Addr.SPRITE_0_X, 96));

        Assert.Equal(expectedCollision, c64.Vic2.SpriteManager.SpriteToSpriteCollisionStore);
    }

    [Theory]
    [InlineData(365, 0, 0)]      // X 365 lies in sprite 0's own fetch (cycles 58-59): never shown
    [InlineData(367, 0x81, 1)]   // the first X after the fetch: shown, and it overlaps sprite 7
    public void Sprite_cannot_start_while_its_own_data_is_fetched(int spriteX, byte expectedCollision, int expectedRuns)
    {
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: (byte)(spriteX - 256), y: 49, spritePointer: 0xF8);
        CreateVisibleSolidSprite(c64, spriteNumber: 7, x: 360 - 256, y: 49, spritePointer: 0xFC);
        c64.WriteIOStorage(Vic2Addr.SPRITE_MSB_X, 0x81);

        DrivePerLineCollisionsForFrame(c64);

        Assert.Equal(expectedCollision, c64.Vic2.SpriteManager.SpriteToSpriteCollisionStore);
        Assert.Equal(expectedRuns, c64.Vic2.SpriteManager.LineSpriteRunCount(56, 0));
    }

    [Fact]
    public void Sprite_still_shifting_at_its_fetch_repeats_its_last_pixel_and_stops()
    {
        // The Demus Interruptus emulator check: sprite 0 with every row $02 (its pixel 22 set) and
        // sprite 1 with every row $55, both at X 332, which never overlap as drawn. Sprite 0 is
        // still shifting when its fetch begins at X 355, so its last shifted pixel, the set one,
        // repeats there and meets sprite 1's pixel 23.
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSprite(c64, spriteNumber: 0, x: 332 - 256, y: 92, spritePointer: 0xF8, Enumerable.Repeat((byte)0x02, 63).ToArray());
        CreateVisibleSprite(c64, spriteNumber: 1, x: 332 - 256, y: 92, spritePointer: 0xFC, Enumerable.Repeat((byte)0x55, 63).ToArray());
        c64.WriteIOStorage(Vic2Addr.SPRITE_MSB_X, 0x03);

        DrivePerLineCollisionsForFrame(c64);

        Assert.Equal(0b0000_0011, c64.Vic2.SpriteManager.SpriteToSpriteCollisionStore);
        var sm = c64.Vic2.SpriteManager;
        Assert.Equal(1, sm.LineSpriteRunCount(100, 0));
        Assert.Equal(332 - 404 + 504, sm.LineSpriteRunStart(100, 0, 0));   // the line's pixel index of X 332
        Assert.Equal(23, sm.LineSpriteRunLength(100, 0, 0));               // shown up to X 354
        Assert.Equal(7, sm.LineSpriteRunStretch(100, 0, 0));               // then repeated through X 361
        Assert.Equal(24, sm.LineSpriteRunLength(100, 1, 0));               // sprite 1's fetch is two cycles on: untouched
    }

    [Fact]
    public void Sprite_row_loaded_by_the_fetch_starts_on_the_same_line()
    {
        // A sprite whose X lies beyond its fetch shows each row on the line the row was fetched
        // on, one line higher than a sprite to the left of the fetch.
        var c64 = BuildC64(perLineSprites: true);
        var shape = new byte[63];
        shape[0] = 0xAA;   // row 0
        shape[3] = 0x55;   // row 1
        CreateVisibleSprite(c64, spriteNumber: 0, x: 380 - 256, y: 100, spritePointer: 0xF8, shape);
        c64.WriteIOStorage(Vic2Addr.SPRITE_MSB_X, 0x01);

        DrivePerLineCollisionsForFrame(c64);

        var sm = c64.Vic2.SpriteManager;
        Assert.Equal(0, sm.LineSpriteRunCount(99, 0));
        Assert.Equal(1, sm.LineSpriteRunCount(100, 0));
        Assert.Equal(380 - 404 + 504, sm.LineSpriteRunStart(100, 0, 0));
        Assert.Equal(0xAA0000u, sm.LineSpriteRunData(100, 0, 0));
        Assert.Equal(0x550000u, sm.LineSpriteRunData(101, 0, 0));
    }

    [Theory]
    [InlineData("PAL", 0, 0)]        // the 6569's line has 504 pixels: X 504 is never reached
    [InlineData("NTSC", 0b11, 1)]    // the 6567R8's has 520: X 504 lies at the line's 92nd pixel
    public void Sprite_X_the_beam_never_reaches_is_never_shown(string model, byte expectedCollision, int expectedRuns)
    {
        var c64 = BuildC64(perLineSprites: true, model);
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 504 - 256, y: 49, spritePointer: 0xF8);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 504 - 256, y: 49, spritePointer: 0xFC);
        c64.WriteIOStorage(Vic2Addr.SPRITE_MSB_X, 0x03);

        DrivePerLineCollisionsForFrame(c64);

        Assert.Equal(expectedCollision, c64.Vic2.SpriteManager.SpriteToSpriteCollisionStore);
        Assert.Equal(expectedRuns, c64.Vic2.SpriteManager.LineSpriteRunCount(56, 0));
    }

    [Theory]
    [InlineData(111, 0x81)]   // moved to X 367, the first X after its fetch: shown again with the next row
    [InlineData(110, 0)]      // moved to X 366, inside the fetch: not shown again
    public void Sprite_moved_past_the_beam_starts_again_after_its_fetch(byte newXLow, byte expectedCollision)
    {
        // VICE's spritex suite, tests 15 and 16 on the 6569: sprite 0 solid at X 256 and sprite 7
        // with only its row 6's first and last pixel at X 367; sprite 0's X rewritten in cycle 45 of
        // row 6's line, after its first run.
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 0, y: 49, spritePointer: 0xF8);
        CreateVisibleSprite(c64, spriteNumber: 7, x: 367 - 256, y: 49, spritePointer: 0xFC, SparseRow6());
        c64.WriteIOStorage(Vic2Addr.SPRITE_MSB_X, 0x81);

        RunFrameWithWriteAt(c64, line: 49 + 1 + 6, cycle: 45, () => c64.Mem.Write(Vic2Addr.SPRITE_0_X, newXLow));

        Assert.Equal(expectedCollision, c64.Vic2.SpriteManager.SpriteToSpriteCollisionStore);
    }

    [Fact]
    public void Collision_latches_the_interrupt_flag_without_the_source_enabled_and_reading_the_register_keeps_it()
    {
        // VICE's spritecollclear test (bug 2135): the flag in $D019 is set whether or not $D01A
        // enables the source, and reading $D01E clears the collision bits but not the flag.
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 72, y: 49, spritePointer: 0xF8);
        CreateVisibleSolidSprite(c64, spriteNumber: 1, x: 80, y: 49, spritePointer: 0xFC);

        DrivePerLineCollisionsForFrame(c64);

        Assert.False(c64.Vic2.Vic2IRQ.IsEnabled(IRQSource.SpriteToSpriteCollision));
        Assert.True(c64.Vic2.Vic2IRQ.IsTriggered(IRQSource.SpriteToSpriteCollision));
        Assert.Equal(0x03, c64.Mem[Vic2Addr.SPRITE_TO_SPRITE_COLLISION]);
        Assert.Equal(0x00, c64.Mem[Vic2Addr.SPRITE_TO_SPRITE_COLLISION]);   // cleared by the read
        Assert.True(c64.Vic2.Vic2IRQ.IsTriggered(IRQSource.SpriteToSpriteCollision));
    }

    // --- The enable register at the compares and the display decision (VICE's spriteenable suite) ---

    [Fact]
    public void Sprite_disabled_after_the_compares_and_before_the_display_decision_fetches_but_does_not_show()
    {
        // The compares in cycles 55 and 56 start the DMA; the display decision in cycle 58 asks
        // for the enable bit again, so a sprite switched off in cycle 57 is fetched but not shown.
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 72, y: 49, spritePointer: 0xF8);

        RunFrameWithWriteAt(c64, line: 49, cycle: 56, () => c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0));

        Assert.Equal(0, c64.Vic2.SpriteManager.LineSpriteDisplayMask(50) & 1);
        Assert.Equal(0, c64.Vic2.SpriteManager.LineSpriteRunCount(50, 0));
    }

    [Fact]
    public void Sprite_enabled_between_the_compares_starts_at_the_second_and_its_first_byte_is_ff()
    {
        // Enabled in cycle 55: the first compare (before the write) misses it, the second starts
        // the DMA. Sprite 0's pointer access follows two cycles later, one short of the three BA
        // takes to stop the CPU, so its first data byte comes back as $FF.
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSprite(c64, spriteNumber: 0, x: 72, y: 49, spritePointer: 0xF8, Enumerable.Repeat((byte)0xAA, 63).ToArray());
        c64.WriteIOStorage(Vic2Addr.SPRITE_ENABLE, 0);

        RunFrameWithWriteAt(c64, line: 49, cycle: 54, () => c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 1));

        var sm = c64.Vic2.SpriteManager;
        Assert.Equal(1, sm.LineSpriteDisplayMask(50) & 1);
        Assert.Equal(new byte[] { 0xFF, 0xAA, 0xAA }, sm.LineSpriteData(50, 0).ToArray());
        Assert.Equal(new byte[] { 0xAA, 0xAA, 0xAA }, sm.LineSpriteData(51, 0).ToArray());
    }

    [Fact]
    public void Sprite_enabled_after_the_second_compare_does_not_start()
    {
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSolidSprite(c64, spriteNumber: 0, x: 72, y: 49, spritePointer: 0xF8);
        c64.WriteIOStorage(Vic2Addr.SPRITE_ENABLE, 0);

        RunFrameWithWriteAt(c64, line: 49, cycle: 55, () => c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 1));

        Assert.Equal(0, c64.Vic2.SpriteManager.LineSpriteDisplayMask(50) & 1);
        Assert.Equal(0, c64.Vic2.SpriteManager.LineSpriteRunCount(51, 0));
    }

    [Fact]
    public void Sprite_restarted_by_the_compare_on_its_last_line_stays_displayed()
    {
        // VICE's spriterestart test: on the last line of a run the DMA ended in cycle 16, so the
        // compare in cycle 55 can start it again when Y names that line; the display decision in
        // cycle 58 then finds the DMA on and leaves the display as it was, on, even though Y has
        // been written back and no longer matches. The next line shows row 0 again.
        var c64 = BuildC64(perLineSprites: true);
        var shape = new byte[63];
        shape[0] = 0xAA;   // row 0
        shape[60] = 0x55;  // row 20
        CreateVisibleSprite(c64, spriteNumber: 0, x: 72, y: 49, spritePointer: 0xF8, shape);

        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        var lastLine = 49 + 21;   // rows 0-20 on lines 50-70
        c64.Vic2.AdvanceRaster((ulong)lastLine * cyclesPerLine + 53);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, (byte)lastLine);     // seen by the compare in cycle 55
        c64.Vic2.AdvanceRaster(2);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, 49);                  // back before the display decision
        c64.Vic2.AdvanceRaster(200 * cyclesPerLine - (ulong)lastLine * cyclesPerLine - 55);

        var sm = c64.Vic2.SpriteManager;
        Assert.Equal(new byte[] { 0x55, 0, 0 }, sm.LineSpriteData(lastLine, 0).ToArray());
        Assert.Equal(1, sm.LineSpriteDisplayMask(lastLine + 1) & 1);
        Assert.Equal(new byte[] { 0xAA, 0, 0 }, sm.LineSpriteData(lastLine + 1, 0).ToArray());
    }

    [Fact]
    public void Sprite_beyond_its_fetch_shown_on_its_first_line_carries_the_idle_bytes()
    {
        // VICE's sb_sprite_fetch tests: a sprite 3-7 with X at or beyond $164 is displayed on the
        // line its compare starts the DMA, before its slot at the next line's start has fetched
        // anything: it shows $FF, the idle byte and $FF, what the slot read while the DMA was off.
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSolidSprite(c64, spriteNumber: 6, x: (byte)(0x164 - 256), y: 49, spritePointer: 0xF8);
        c64.WriteIOStorage(Vic2Addr.SPRITE_MSB_X, 0x40);
        c64.Vic2.Vic2Mem[0x3FFF] = 0x33;

        DrivePerLineCollisionsForFrame(c64);

        var sm = c64.Vic2.SpriteManager;
        Assert.Equal(1, sm.LineSpriteRunCount(49, 6));
        Assert.Equal(0xFF33FFu, sm.LineSpriteRunData(49, 6, 0));
        Assert.Equal(0xFFFFFFu, sm.LineSpriteRunData(50, 6, 0));
    }

    [Theory]
    [InlineData(0x3F, 0x00, 3)]   // both bank bits outputs, written 00: bank 3
    [InlineData(0x3C, 0x00, 0)]   // both inputs: the pull-ups read 11, bank 0 whatever was written
    [InlineData(0x3D, 0x00, 1)]   // bit 0 output 0, bit 1 input 1: %10, bank 1
    public void Vic_bank_follows_the_pins_of_cia2_port_a(byte ddr, byte port, int expectedBank)
    {
        // VICE's banking test: a bit of $DD00 made an input by $DD02 floats up through its
        // pull-up, so programs select the bank by writing the direction register as well.
        var c64 = BuildC64();
        c64.Mem.Write(0xDD02, ddr);
        c64.Mem.Write(0xDD00, port);
        Assert.Equal(expectedBank, c64.Vic2.CurrentVIC2Bank);
        c64.Mem.Write(0xDD02, 0x3F);   // back to outputs: the written value counts again
        Assert.Equal(3, c64.Vic2.CurrentVIC2Bank);
    }

    // --- Register changes while a sprite shifts (VICE's spritesplit suite) ---

    private static readonly int[] s_noHalt = Array.Empty<int>();

    private static byte[] Decode(uint register, bool multiColor, bool xExpand, int[] eventPixels, byte[] eventKinds, int haltPixel = int.MaxValue, int stopPixel = int.MaxValue)
    {
        var pixels = new byte[Vic2SpriteManager.RunPixelCapacity];
        var count = Vic2SpriteManager.DecodeSpriteRun(register, 0, haltPixel, stopPixel, multiColor, xExpand, false, eventPixels, eventKinds, pixels);
        return pixels.AsSpan(0, count).ToArray();
    }

    private static byte Event(byte kind, bool set) => (byte)(kind | (set ? Vic2SpriteManager.RunEventBitSet : 0));

    [Fact]
    public void Multicolour_switched_on_while_shifting_takes_the_pairs_from_the_shapes_odd_bits()
    {
        // Row bits 0110...: single colour shows 0, 2, 2, 0. With multicolour from pixel 1 the pair
        // flip-flop is cleared there, so the next pair is taken a pixel late, from bits 21-20
        // instead of 22-21: the sprite split realignment.
        var pixels = Decode(0x600000, multiColor: false, xExpand: false, new[] { 1 }, new[] { Event(Vic2SpriteManager.RunEventMultiColor, true) });
        Assert.Equal(new byte[] { 0, 0, 2, 2, 0 }, pixels);
    }

    [Fact]
    public void X_expand_set_while_shifting_repeats_the_pixel_being_shown()
    {
        // Row bits 1010...: 2, 0, 2, 0 unexpanded. Expanded from pixel 1, the expansion flip-flop
        // starts toggling there: the pixel fetched at 1 is shown twice, and so is every later one.
        var pixels = Decode(0xA00000, multiColor: false, xExpand: false, new[] { 1 }, new[] { Event(Vic2SpriteManager.RunEventXExpand, true) });
        Assert.Equal(new byte[] { 2, 0, 0, 2, 2, 0 }, pixels);
    }

    [Fact]
    public void X_expand_cleared_while_shifting_fetches_the_next_pixel_at_once()
    {
        // Expanded 1010... shows 2, 2, 0, 0, 2, 2, 0, 0. Cleared from pixel 2 the flip-flop is held
        // set, so from there each bit shows once.
        var pixels = Decode(0xA00000, multiColor: false, xExpand: true, new[] { 2 }, new[] { Event(Vic2SpriteManager.RunEventXExpand, false) });
        Assert.Equal(new byte[] { 2, 2, 0, 2, 0 }, pixels);
    }

    [Fact]
    public void Priority_is_read_at_every_pixel()
    {
        var pixels = Decode(0xF00000, multiColor: false, xExpand: false, new[] { 2 }, new[] { Event(Vic2SpriteManager.RunEventPriority, true) });
        var behind = Vic2SpriteManager.RunPixelBehindForeground;
        Assert.Equal(new byte[] { 2, 2, (byte)(2 | behind), (byte)(2 | behind), behind }, pixels);
    }

    [Fact]
    public void Halted_sprite_repeats_its_last_pixel_until_it_is_switched_off()
    {
        var pixels = Decode(0xFFFFFF, multiColor: false, xExpand: false, s_noHalt, Array.Empty<byte>(), haltPixel: 2, stopPixel: 5);
        Assert.Equal(new byte[] { 2, 2, 2, 2, 2 }, pixels);
    }

    [Fact]
    public void Multicolour_written_while_a_sprite_shifts_is_seen_from_the_cycles_fourth_pixel()
    {
        // Sprite 0 at X 96 (the line's pixel 196), rows 01010101...: single colour shows the odd
        // bits. Multicolour is switched on in cycle 25, seen from its pixel 3, the run's pixel 7:
        // that pixel repeats the last one, then every pair comes from bits (8, 9), (10, 11), ...,
        // each 01, the first shared colour, until the register is empty.
        var c64 = BuildC64(perLineSprites: true);
        CreateVisibleSprite(c64, spriteNumber: 0, x: 96, y: 49, spritePointer: 0xF8, Enumerable.Repeat((byte)0x55, 63).ToArray());

        RunFrameWithWriteAt(c64, line: 49 + 1 + 6, cycle: 25, () => c64.Mem.Write(Vic2Addr.SPRITE_MULTICOLOR_ENABLE, 1));

        var sm = c64.Vic2.SpriteManager;
        Assert.Equal(1, sm.LineSpriteRunCount(56, 0));
        Assert.Equal(196, sm.LineSpriteRunStart(56, 0, 0));
        Assert.NotEqual(0, sm.LineSpriteRunFlags(56, 0, 0) & Vic2SpriteManager.RunFlagDecoded);
        var expected = new byte[] { 0, 2, 0, 2, 0, 2, 0, 0 }.Concat(Enumerable.Repeat((byte)1, 16)).Append((byte)0).ToArray();
        Assert.Equal(expected, sm.LineSpriteRunPixels(56, 0, 0).ToArray());
        // The line before the write is an ordinary single-colour run.
        Assert.Equal(0, sm.LineSpriteRunFlags(55, 0, 0) & (Vic2SpriteManager.RunFlagDecoded | Vic2SpriteManager.RunFlagMultiColor));
        Assert.Equal(24, sm.LineSpriteRunLength(55, 0, 0));
    }

    // A sprite with only its row 6's first and last pixel set.
    private static byte[] SparseRow6()
    {
        var sparse = new byte[63];
        sparse[6 * 3] = 0x80;
        sparse[6 * 3 + 2] = 0x01;
        return sparse;
    }

    // Drives the raster from the frame's start to the given cycle (0-based) of the given line,
    // performs the write there, then on to line 200: past the sprites, but short of line 256,
    // where the Y compare (on the raster's low byte) would start them again with the new X.
    private static void RunFrameWithWriteAt(C64 c64, int line, int cycle, Action write)
    {
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        var at = (ulong)line * cyclesPerLine + (ulong)cycle;
        c64.Vic2.AdvanceRaster(at);
        write();
        c64.Vic2.AdvanceRaster(200 * cyclesPerLine - at);
    }

    private static C64 BuildC64(bool perLineSprites = false, string model = "PAL")
    {
        return C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = model == "PAL" ? "C64PAL" : "C64NTSC",
            Vic2Model = model,
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
