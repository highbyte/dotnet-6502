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

    [Fact]
    public void DrawText_resumes_on_character_row_boundary_after_invalid_band_with_vertical_fine_scroll()
    {
        var c64 = BuildC64();
        SetupRowBoundaryMarkerTextScreen(c64);
        // The generator's internal drawLine is relative to the Visible layout's screen start; the
        // rendered pixel position is relative to the VisibleNormalized layout's screen start.
        var visibleLayout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.Visible, for24RowMode: false, for38ColMode: false);
        var normalizedLayout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        const int bandStartDrawLine = 100;
        const int resumeDrawLine = 116;
        // SCROLLY=5 (= +2 relative to the default 3) below the band; invalid mode = ECM+BMM.
        var (_, foreground) = RenderFrame(c64, rasterLine =>
        {
            var drawLine = c64.Vic2.Vic2Model.ConvertRasterLineToScreenLine(rasterLine) - visibleLayout.Screen.Start.Y;
            if (drawLine < bandStartDrawLine)
                return 0x1B;
            if (drawLine < resumeDrawLine)
                return 0x7B;
            return 0x1D;
        });

        // The character grid snap must include the resume line's vertical fine scroll (SCROLLY=5 =>
        // snap offset 2, drawing the resumed area from pixel row resumeDrawLine - 2). Without it the
        // snap is 4, which draws the tail glyph lines of the character row the band should hide over
        // the band's bottom rows - the garbled seam seen in e.g. Commando.
        var width = c64.Screen.VisibleWidth;
        var bandTopY = normalizedLayout.Screen.Start.Y + bandStartDrawLine;
        var firstResumedY = normalizedLayout.Screen.Start.Y + resumeDrawLine - 2;
        for (var y = bandTopY; y < firstResumedY; y++)
        {
            for (var x = normalizedLayout.Screen.Start.X; x <= normalizedLayout.Screen.End.X; x++)
            {
                Assert.Equal(0u, foreground[y * width + x]);
            }
        }

        // The first resumed row must start on a character-row boundary: glyph line 0 is solid.
        for (var x = normalizedLayout.Screen.Start.X; x < normalizedLayout.Screen.Start.X + 8; x++)
        {
            Assert.NotEqual(0u, foreground[firstResumedY * width + x]);
        }
    }

    [Fact]
    public void DrawText_keeps_a_character_rows_screen_codes_from_its_first_line_as_the_vic2_latches_them()
    {
        // The VIC-II fetches a character row's screen codes and colour nibbles once, on the row's
        // first line, and shows them for the row's remaining lines. A screen write made after that
        // fetch appears in the next row (whose fetch is still ahead), not in the current one.
        var c64 = BuildC64();
        SetupRowBoundaryMarkerTextScreen(c64);
        c64.Mem.Write(0xD011, 0x1B);   // display on, SCROLLY=3 (the normal position): row r begins on draw line 8r + 3
        var visibleLayout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.Visible, for24RowMode: false, for38ColMode: false);
        var normalizedLayout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        const int col = 5;

        var (_, foreground) = RenderFrame(c64, beforeRasterLine: rasterLine =>
        {
            var drawLine = c64.Vic2.Vic2Model.ConvertRasterLineToScreenLine(rasterLine) - visibleLayout.Screen.Start.Y;
            if (drawLine != 3 + 3)
                return;
            // Row 0 is three lines into its display; row 1 has not been fetched yet.
            c64.Vic2.Vic2Mem[(ushort)(0x0400 + 0 * 40 + col)] = 2;   // solid glyph
            c64.Vic2.Vic2Mem[(ushort)(0x0400 + 1 * 40 + col)] = 2;
        });

        var width = c64.Screen.VisibleWidth;
        var x = normalizedLayout.Screen.Start.X + col * 8 + 3;   // an interior pixel: blank in code 1's lines 1-7, set in code 2
        // The normalized layout places row 0's first line at its screen start (SCROLLY=3 is the norm).
        var row0Line5 = normalizedLayout.Screen.Start.Y + 5;
        var row1Line5 = normalizedLayout.Screen.Start.Y + 8 + 5;
        Assert.Equal(0u, foreground[row0Line5 * width + x]);      // row 0 still shows the code it was fetched with
        Assert.NotEqual(0u, foreground[row1Line5 * width + x]);   // row 1 was fetched after the write
    }

    [Fact]
    public void DrawText_does_not_sample_below_last_character_row_when_fine_scrolled_up()
    {
        var c64 = BuildC64();
        SetupRowBoundaryMarkerTextScreen(c64);
        var layout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        // Fill the memory directly after the 1000-byte video matrix with characters that render
        // solid pixels on every glyph line, so sampling past character row 24 becomes visible.
        for (var i = 0; i < 40; i++)
        {
            c64.Vic2.Vic2Mem[(ushort)(0x0400 + 1000 + i)] = 2;
        }

        // SCROLLY=0 (= -3 relative to the default 3) on all lines samples 3 lines ahead.
        var (_, foreground) = RenderFrame(c64, _ => 0x18);

        // The last 3 main screen lines have no character row to sample; they must stay blank
        // instead of rendering data from beyond the video matrix.
        var width = c64.Screen.VisibleWidth;
        for (var drawLine = 197; drawLine < 200; drawLine++)
        {
            var y = layout.Screen.Start.Y + drawLine;
            for (var x = layout.Screen.Start.X; x <= layout.Screen.End.X; x++)
            {
                Assert.Equal(0u, foreground[y * width + x]);
            }
        }
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

    [Theory]
    [InlineData(5)]    // a top border line: the whole line is border
    [InlineData(60)]   // a main screen line: only the side borders show the border colour
    public void Border_colour_written_mid_line_changes_from_the_pixel_block_after_the_write(int normalizedLine)
    {
        var c64 = BuildC64();
        SetupRowBoundaryMarkerTextScreen(c64);
        c64.Mem.Write(0xD011, 0x1B);
        c64.Mem.Write(0xD020, 0);
        const int writeCycle = 30;   // cycles into the line completed when the write lands
        var (generator, background, _) = CreateGenerator(c64);

        RenderFrameWithMidLineWrite(c64, generator, normalizedLine, writeCycle, () => c64.Mem.Write(0xD020, 0xF2));   // unused high bits are ignored

        var width = c64.Screen.VisibleWidth;
        var visibleLayout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.Visible, for24RowMode: false, for38ColMode: false);
        var normalizedLayout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        var changeX = (writeCycle + 1) * 8 - visibleLayout.LeftBorder.Start.X + c64.Vic2.Vic2Model.ColorChangePixelDelay;   // first pixel the change shows on
        var lineStart = normalizedLine * width;
        var oldColor = background[lineStart];
        var newColor = background[lineStart + width - 1];
        Assert.NotEqual(oldColor, newColor);
        if (normalizedLine <= normalizedLayout.TopBorder.End.Y)
        {
            Assert.Equal(oldColor, background[lineStart + changeX - 1]);
            Assert.Equal(newColor, background[lineStart + changeX]);
        }
        else
        {
            // The change lands inside the main screen area: the left border keeps the old colour
            // and the right border has the new one.
            Assert.True(changeX > normalizedLayout.LeftBorder.End.X && changeX < normalizedLayout.RightBorder.Start.X);
            Assert.Equal(oldColor, background[lineStart + normalizedLayout.LeftBorder.End.X]);
            Assert.Equal(newColor, background[lineStart + normalizedLayout.RightBorder.Start.X]);
        }
    }

    [Fact]
    public void Background_colour_written_mid_line_splits_the_standard_text_background_at_the_write()
    {
        var c64 = BuildC64();
        SetupRowBoundaryMarkerTextScreen(c64);
        c64.Mem.Write(0xD011, 0x1B);
        c64.Mem.Write(0xD021, 6);
        const int writeCycle = 30;
        var (generator, background, _) = CreateGenerator(c64);
        var normalizedLayout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        var normalizedLine = normalizedLayout.Screen.Start.Y + 5;

        RenderFrameWithMidLineWrite(c64, generator, normalizedLine, writeCycle, () => c64.Mem.Write(0xD021, 0));

        var width = c64.Screen.VisibleWidth;
        var visibleLayout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.Visible, for24RowMode: false, for38ColMode: false);
        var changeX = (writeCycle + 1) * 8 - visibleLayout.LeftBorder.Start.X + c64.Vic2.Vic2Model.ColorChangePixelDelay;
        Assert.True(changeX > normalizedLayout.Screen.Start.X && changeX < normalizedLayout.Screen.End.X);
        var lineStart = normalizedLine * width;
        var oldColor = background[lineStart + normalizedLayout.Screen.Start.X];
        var newColor = background[lineStart + normalizedLayout.Screen.End.X];
        Assert.NotEqual(oldColor, newColor);
        Assert.Equal(oldColor, background[lineStart + changeX - 1]);
        Assert.Equal(newColor, background[lineStart + changeX]);
        // The line below, drawn entirely after the write, has the new colour throughout.
        Assert.Equal(newColor, background[(normalizedLine + 1) * width + normalizedLayout.Screen.Start.X]);
    }

    [Theory]
    [InlineData("C64PAL", "PAL", 124)]
    [InlineData("C64NTSC", "NTSC", 124)]
    public void Border_colour_written_on_a_cycle_lands_where_the_chip_puts_that_cycle(string c64Model, string vic2Model, int displayWindowStartX)
    {
        // Pins the mapping from cycle to pixel against the chip figures rather than against the
        // layout: a write completing on cycle c is shown from the start of cycle c + 1, which is
        // (c + 1) * 8 pixels into the line, and the display window's first pixel is
        // displayWindowStartX pixels into the line. So the change lands that far into the window.
        var c64 = BuildC64(c64Model, vic2Model);
        SetupRowBoundaryMarkerTextScreen(c64);
        c64.Mem.Write(0xD011, 0x1B);
        c64.Mem.Write(0xD020, 0);
        const int writeCycle = 30;
        var (generator, background, _) = CreateGenerator(c64);
        var normalizedLayout = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        var normalizedLine = 5;   // a top border line, where the whole line carries the border colour

        RenderFrameWithMidLineWrite(c64, generator, normalizedLine, writeCycle, () => c64.Mem.Write(0xD020, 2));

        var width = c64.Screen.VisibleWidth;
        var lineStart = normalizedLine * width;
        // Pixels into the display window, which can be negative: the change falls in the left border.
        var changeIntoDisplayWindow = (writeCycle + 1) * 8 - displayWindowStartX + c64.Vic2.Vic2Model.ColorChangePixelDelay;
        var changeX = normalizedLayout.Screen.Start.X + changeIntoDisplayWindow;
        Assert.NotEqual(background[lineStart + changeX - 1], background[lineStart + changeX]);
        Assert.Equal(background[lineStart], background[lineStart + changeX - 1]);
    }

    [Fact]
    public void Border_colour_written_on_every_line_of_a_frame_keeps_landing_at_its_own_cycle()
    {
        // A frame's worth of colour changes is far more than the write journal holds at once, so
        // the journal has to be emptied as the writes are applied rather than filling up.
        var c64 = BuildC64();
        SetupRowBoundaryMarkerTextScreen(c64);
        c64.Mem.Write(0xD011, 0x1B);
        const int writeCycle = 30;
        var (generator, background, _) = CreateGenerator(c64);
        var vic2 = c64.Vic2;
        var cyclesPerLine = vic2.Vic2Model.CyclesPerLine;
        var visibleLayout = vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.Visible, for24RowMode: false, for38ColMode: false);

        // Every line writes a different border colour partway along the line.
        for (var rasterLine = 0; rasterLine < vic2.Vic2Model.TotalHeight; rasterLine++)
        {
            vic2.AdvanceRaster((ulong)writeCycle);
            c64.Mem.Write(0xD020, (byte)(rasterLine % 16));
            vic2.AdvanceRaster(cyclesPerLine - (ulong)writeCycle);
            generator.OnAfterInstruction();
        }

        // Every line of the top border must show its own two colours, split at the write.
        var width = c64.Screen.VisibleWidth;
        var changeX = (writeCycle + 1) * 8 - visibleLayout.LeftBorder.Start.X + c64.Vic2.Vic2Model.ColorChangePixelDelay;
        var checkedLines = 0;
        for (var line = 1; line < 30; line++)
        {
            var rasterLine = line + visibleLayout.TopBorder.Start.Y;
            var previous = (byte)((rasterLine - 1) % 16);
            var current = (byte)(rasterLine % 16);
            if (previous == current)
                continue;
            Assert.NotEqual(background[line * width + changeX - 1], background[line * width + changeX]);
            checkedLines++;
        }
        Assert.True(checkedLines > 20, $"expected most lines to be checked, was {checkedLines}");
    }

    [Fact]
    public void Colour_registers_set_without_the_memory_map_are_picked_up_at_the_end_of_the_frame()
    {
        var c64 = BuildC64();
        SetupRowBoundaryMarkerTextScreen(c64);
        c64.Mem.Write(0xD011, 0x1B);
        c64.Mem.Write(0xD020, 0);
        var (generator, background, _) = CreateGenerator(c64);
        var width = c64.Screen.VisibleWidth;

        // Bypasses the register write journal (as a snapshot restore does).
        c64.WriteIOStorage(Vic2Addr.BORDER_COLOR, 2);
        RenderFrameWithMidLineWrite(c64, generator, normalizedLine: -1, writeCycle: 0, write: null);
        var firstFrameBorder = background[5 * width];
        generator.OnEndFrame();
        RenderFrameWithMidLineWrite(c64, generator, normalizedLine: -1, writeCycle: 0, write: null);
        var secondFrameBorder = background[5 * width];

        Assert.NotEqual(firstFrameBorder, secondFrameBorder);
    }

    /// <summary>
    /// Drives one frame a line at a time like <see cref="RenderFrame"/>, but on the given normalized
    /// screen line performs <paramref name="write"/> after <paramref name="writeCycle"/> cycles of
    /// the line, with the line's pixels generated in a single pass afterwards (as the emulator does
    /// for an instruction that spans the write).
    /// </summary>
    private static void RenderFrameWithMidLineWrite(C64 c64, Vic2RasterizerUintPixelGenerator generator, int normalizedLine, int writeCycle, Action? write)
    {
        var vic2 = c64.Vic2;
        var cyclesPerLine = vic2.Vic2Model.CyclesPerLine;
        var visibleLayout = vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.Visible, for24RowMode: false, for38ColMode: false);
        for (var rasterLine = 0; rasterLine < vic2.Vic2Model.TotalHeight; rasterLine++)
        {
            var screenLine = vic2.Vic2Model.ConvertRasterLineToScreenLine(rasterLine);
            if (write != null && screenLine - visibleLayout.TopBorder.Start.Y == normalizedLine)
            {
                vic2.AdvanceRaster((ulong)writeCycle);
                write();
                vic2.AdvanceRaster(cyclesPerLine - (ulong)writeCycle);
            }
            else
            {
                vic2.AdvanceRaster(cyclesPerLine);
            }
            generator.OnAfterInstruction();
        }
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
        var (generator, _, foreground) = CreateGenerator(c64);
        generator.DrawSpritesToBitmapBackedByPixelArray();
        return foreground;
    }

    private static (Vic2RasterizerUintPixelGenerator Generator, uint[] Background, uint[] Foreground) CreateGenerator(C64 c64)
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
        return (generator, background, foreground);
    }

    /// <summary>
    /// Standard text mode screen where every character cell renders a solid foreground line on
    /// glyph line 0 and a sparse-but-visible pattern on glyph lines 1-7, making both character-row
    /// boundaries and partial (mid-row) glyph lines observable.
    /// Character code 2 renders solid pixels on all glyph lines (for out-of-matrix detection).
    /// </summary>
    private static void SetupRowBoundaryMarkerTextScreen(C64 c64)
    {
        // 40-column mode, no horizontal fine scroll (ROMs are not loaded, so nothing else sets it).
        c64.Mem.Write(0xD016, 0xC8);
        // Video matrix at 0x0400, charset at 0x2000 (plain RAM, avoids the char ROM shadow).
        c64.Mem.Write(0xD018, 0x18);
        var charsetAddress = (ushort)0x2000;
        c64.Vic2.Vic2Mem[(ushort)(charsetAddress + 1 * 8)] = 0xFF;
        for (var i = 1; i < 8; i++)
        {
            c64.Vic2.Vic2Mem[(ushort)(charsetAddress + 1 * 8 + i)] = 0x81;
        }
        for (var i = 0; i < 8; i++)
        {
            c64.Vic2.Vic2Mem[(ushort)(charsetAddress + 2 * 8 + i)] = 0xFF;
        }

        for (var i = 0; i < 1000; i++)
        {
            c64.Vic2.Vic2Mem[(ushort)(0x0400 + i)] = 1;
            c64.WriteIOStorage((ushort)(Vic2Addr.COLOR_RAM_START + i), 1); // White
        }
    }

    /// <summary>
    /// Drives the pixel generator through one full frame the same way the emulator main loop does:
    /// advance the raster one line at a time and let the generator process the elapsed cycles.
    /// The optional callback supplies the $D011 value in effect while each raster line renders.
    /// </summary>
    private static (uint[] Background, uint[] Foreground) RenderFrame(C64 c64, Func<int, byte>? d011ForRasterLine = null, Action<int>? beforeRasterLine = null)
    {
        var (generator, background, foreground) = CreateGenerator(c64);
        var vic2 = c64.Vic2;
        var cyclesPerLine = vic2.Vic2Model.CyclesPerLine;
        for (var rasterLine = 0; rasterLine < vic2.Vic2Model.TotalHeight; rasterLine++)
        {
            if (d011ForRasterLine != null)
                c64.Mem.Write(0xD011, d011ForRasterLine(rasterLine));
            beforeRasterLine?.Invoke(rasterLine);
            vic2.AdvanceRaster(cyclesPerLine);
            generator.OnAfterInstruction();
        }
        return (background, foreground);
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
