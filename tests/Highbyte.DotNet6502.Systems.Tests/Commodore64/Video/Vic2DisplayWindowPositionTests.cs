using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

/// <summary>
/// Where the display window sits in a raster line. The VIC-II's display window is at X 24-343 of its
/// own coordinate system on every chip variant, and the X coordinate at the start of a line's first
/// cycle differs per variant, but so does where the X counter wraps (504 on the 6569, 512 on the
/// 6567R8 whose line is 520 pixels), and X 0 ends up 100 pixels after the first cycle's start on
/// both, so the window sits 124 pixels into the line on both. Everything the rasterizer derives from
/// a cycle is placed relative to this, so it decides where raster timed effects land.
/// </summary>
public class Vic2DisplayWindowPositionTests
{
    [Theory]
    [InlineData("C64PAL", "PAL", 124)]
    [InlineData("C64NTSC", "NTSC", 124)]
    public void Display_window_starts_at_the_chip_variants_pixel_offset_into_the_raster_line(string c64Model, string vic2Model, int expectedStartX)
    {
        var c64 = BuildC64(c64Model, vic2Model);

        var wholeLine = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.Normal, for24RowMode: false, for38ColMode: false);

        Assert.Equal(expectedStartX, wholeLine.Screen.Start.X);
        Assert.Equal(expectedStartX + c64.Vic2.Vic2Screen.DrawableAreaWidth - 1, wholeLine.Screen.End.X);
    }

    [Theory]
    [InlineData("C64PAL", "PAL", 124)]
    [InlineData("C64NTSC", "NTSC", 124)]
    public void Visible_area_is_placed_so_the_display_window_keeps_its_offset_into_the_line(string c64Model, string vic2Model, int expectedStartX)
    {
        var c64 = BuildC64(c64Model, vic2Model);
        var screen = c64.Vic2.Vic2Screen;

        var visible = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.Visible, for24RowMode: false, for38ColMode: false);

        Assert.Equal(expectedStartX, visible.Screen.Start.X);
        Assert.Equal(expectedStartX - screen.VisibleLeftRightBorderWidth, visible.LeftBorder.Start.X);
        Assert.Equal(screen.VisibleWidth, visible.RightBorder.End.X - visible.LeftBorder.Start.X + 1);
    }

    [Theory]
    [InlineData("C64PAL", "PAL", 41, 42)]
    [InlineData("C64NTSC", "NTSC", 49, 49)]
    public void Border_widths_within_the_visible_area_are_unchanged_by_where_the_line_starts(string c64Model, string vic2Model, int expectedLeftBorderWidth, int expectedRightBorderWidth)
    {
        // The visible frame and the display window's place inside it are a presentation choice (real
        // sets showed different amounts of border); only the offset into the line above is fixed by
        // the chip. This pins the choice so the anchor cannot move the picture within the frame.
        var c64 = BuildC64(c64Model, vic2Model);
        var screen = c64.Vic2.Vic2Screen;

        var normalized = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        Assert.Equal(0, normalized.LeftBorder.Start.X);
        Assert.Equal(expectedLeftBorderWidth, normalized.Screen.Start.X);
        Assert.Equal(expectedRightBorderWidth, screen.VisibleWidth - (normalized.Screen.End.X + 1));
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
}
