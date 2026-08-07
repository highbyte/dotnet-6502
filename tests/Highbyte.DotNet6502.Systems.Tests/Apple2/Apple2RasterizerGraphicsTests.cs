using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Rasterizer tests for the graphics modes (lo-res, hi-res, mixed) selected by the display
/// soft switches. Text-mode rendering itself is covered by <see cref="Apple2RasterizerTests"/>.
/// </summary>
public class Apple2RasterizerGraphicsTests
{
    /// <summary>
    /// Defaults to a phosphor monitor so the hi-res tests below see the raw dot pattern. Artifact
    /// colour, which reinterprets those same dots, is covered by
    /// <see cref="Apple2RasterizerHiResColorTests"/>.
    /// </summary>
    private static Apple2System BuildApple2(Apple2MonitorColor monitorColor = Apple2MonitorColor.Green)
    {
        // An all-zero character generator: text cells render blank except inverse video, which
        // lights the whole 7x8 cell — handy for asserting where text rows land in mixed mode.
        var romData = new Dictionary<string, byte[]>
        {
            { Apple2SystemConfig.CHARGEN_ROM_NAME, new byte[Apple2CharSet.CharacterRomSize] },
        };
        return new Apple2System(
            new Apple2Config { MonitorColor = monitorColor },
            NullLoggerFactory.Instance,
            romData);
    }

    private static Apple2Rasterizer GetRasterizer(Apple2System apple2)
        => (Apple2Rasterizer)apple2.RenderProvider!;

    /// <summary>
    /// Fills the text page with normal-video spaces. Unwritten RAM reads as $00, which is an
    /// <em>inverse</em> '@' — a lit cell — so tests that render text rows must clear first.
    /// </summary>
    private static void ClearTextScreen(Apple2System apple2)
    {
        var space = Apple2CharSet.FromAscii((byte)' ');
        for (var row = 0; row < Apple2Config.Rows; row++)
            for (var col = 0; col < Apple2Config.Cols; col++)
                apple2.Mem[Apple2TextScreen.GetAddress(row, col)] = space;
    }

    private static uint PixelAt(Apple2Rasterizer rasterizer, int x, int y)
        => rasterizer.CurrentFrontLayerBuffers[1].Span[(y * rasterizer.NativeSize.Width) + x];

    private static uint Packed(System.Drawing.Color color)
        => Apple2Rasterizer.PackBgra(color.B, color.G, color.R, color.A);

    private static uint Foreground(Apple2System apple2)
        => Packed(Apple2Colors.GetForeground(apple2.Apple2Config.MonitorColor));

    private static void SelectMode(Apple2System apple2, bool text, bool hiRes = false, bool mixed = false, bool page2 = false)
    {
        _ = apple2.Mem[text ? Apple2SoftSwitches.TextModeAddress : Apple2SoftSwitches.GraphicsModeAddress];
        _ = apple2.Mem[hiRes ? Apple2SoftSwitches.HiResModeAddress : Apple2SoftSwitches.LoResModeAddress];
        _ = apple2.Mem[mixed ? Apple2SoftSwitches.MixedModeOnAddress : Apple2SoftSwitches.MixedModeOffAddress];
        _ = apple2.Mem[page2 ? Apple2SoftSwitches.TextPage2Address : Apple2SoftSwitches.TextPage1Address];
    }

    [Fact]
    public void HiRes_Bits_0_To_6_Map_To_Pixels_Left_To_Right()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectMode(apple2, text: false, hiRes: true);

        apple2.Mem[Apple2HiResScreen.HiResPage1BaseAddress] = 0b0100_0001;   // pixels 0 and 6
        rasterizer.OnEndFrame();

        var foreground = Foreground(apple2);
        Assert.Equal(foreground, PixelAt(rasterizer, 0, 0));
        Assert.Equal(0u, PixelAt(rasterizer, 1, 0));
        Assert.Equal(0u, PixelAt(rasterizer, 5, 0));
        Assert.Equal(foreground, PixelAt(rasterizer, 6, 0));
    }

    [Fact]
    public void HiRes_Bit_7_Does_Not_Light_Any_Pixel()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectMode(apple2, text: false, hiRes: true);

        apple2.Mem[Apple2HiResScreen.HiResPage1BaseAddress] = 0x80;
        rasterizer.OnEndFrame();

        for (var x = 0; x < Apple2HiResScreen.PixelsPerByte; x++)
            Assert.Equal(0u, PixelAt(rasterizer, x, 0));
    }

    [Fact]
    public void HiRes_Interleaved_Lines_Land_On_The_Right_Scan_Lines()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectMode(apple2, text: false, hiRes: true);

        // Line 1 lives at $2400, not 40 bytes into the page.
        apple2.Mem[Apple2HiResScreen.GetLineStartAddress(1)] = 0x01;
        rasterizer.OnEndFrame();

        Assert.NotEqual(0u, PixelAt(rasterizer, 0, 1));
        for (var y = 0; y < Apple2HiResScreen.Lines; y++)
            if (y != 1)
                Assert.Equal(0u, PixelAt(rasterizer, 0, y));
    }

    [Fact]
    public void HiRes_Second_Byte_Of_A_Line_Starts_At_Pixel_7()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectMode(apple2, text: false, hiRes: true);

        apple2.Mem[Apple2HiResScreen.HiResPage1BaseAddress + 1] = 0x01;
        rasterizer.OnEndFrame();

        Assert.Equal(0u, PixelAt(rasterizer, 6, 0));
        Assert.NotEqual(0u, PixelAt(rasterizer, 7, 0));
    }

    [Fact]
    public void The_Page2_Soft_Switch_Flips_Between_The_HiRes_Pages()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectMode(apple2, text: false, hiRes: true);

        apple2.Mem[Apple2HiResScreen.HiResPage2BaseAddress] = 0x01;
        rasterizer.OnEndFrame();
        Assert.Equal(0u, PixelAt(rasterizer, 0, 0));   // page 1 is blank

        _ = apple2.Mem[Apple2SoftSwitches.TextPage2Address];
        rasterizer.OnEndFrame();
        Assert.NotEqual(0u, PixelAt(rasterizer, 0, 0));

        _ = apple2.Mem[Apple2SoftSwitches.TextPage1Address];
        rasterizer.OnEndFrame();
        Assert.Equal(0u, PixelAt(rasterizer, 0, 0));
    }

    [Fact]
    public void LoRes_Renders_Both_Nibbles_As_Stacked_7x4_Color_Blocks()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectMode(apple2, text: false, hiRes: false);

        // Upper block magenta (1), lower block white (15).
        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = 0xF1;
        rasterizer.OnEndFrame();

        var magenta = Packed(Apple2LoResScreen.Palette[1]);
        var white = Packed(Apple2LoResScreen.Palette[15]);
        for (var x = 0; x < Apple2LoResScreen.BlockPixelWidth; x++)
        {
            for (var y = 0; y < Apple2LoResScreen.BlockPixelHeight; y++)
            {
                Assert.Equal(magenta, PixelAt(rasterizer, x, y));
                Assert.Equal(white, PixelAt(rasterizer, x, y + Apple2LoResScreen.BlockPixelHeight));
            }
        }

        // The neighbouring cell is black (memory reads 0), i.e. transparent foreground.
        Assert.Equal(0u, PixelAt(rasterizer, Apple2LoResScreen.BlockPixelWidth, 0));
    }

    [Fact]
    public void LoRes_Black_Blocks_Leave_The_Foreground_Transparent()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectMode(apple2, text: false, hiRes: false);

        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = 0x00;
        rasterizer.OnEndFrame();

        Assert.Equal(0u, PixelAt(rasterizer, 0, 0));
        Assert.Equal(Packed(Apple2Colors.Background),
            rasterizer.CurrentFrontLayerBuffers[0].Span[0]);
    }

    [Fact]
    public void Mixed_Mode_Renders_Graphics_Above_The_Bottom_Four_Text_Rows()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectMode(apple2, text: false, hiRes: true, mixed: true);
        ClearTextScreen(apple2);

        // Hi-res data on the last graphics line and on the first line of the text area.
        apple2.Mem[Apple2HiResScreen.GetLineStartAddress(Apple2Config.MixedModeGraphicsHeight - 1)] = 0x01;
        apple2.Mem[Apple2HiResScreen.GetLineStartAddress(Apple2Config.MixedModeGraphicsHeight)] = 0x01;
        // An inverse space in the first mixed text row lights its whole cell.
        apple2.Mem[Apple2TextScreen.GetAddress(Apple2Config.MixedModeFirstTextRow, 0)] =
            Apple2CharSet.FromAscii((byte)' ', Apple2TextAttribute.Inverse);

        rasterizer.OnEndFrame();

        Assert.NotEqual(0u, PixelAt(rasterizer, 0, Apple2Config.MixedModeGraphicsHeight - 1));
        // The text area shows text, not the hi-res data behind it.
        var textAreaTop = Apple2Config.MixedModeGraphicsHeight;
        Assert.Equal(Foreground(apple2), PixelAt(rasterizer, 0, textAreaTop));
        // A cell without text in the text area is blank even though hi-res memory has data there.
        Assert.Equal(0u, PixelAt(rasterizer, 8, textAreaTop));
    }

    [Fact]
    public void Mixed_LoRes_Stops_The_Blocks_At_The_Text_Area()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectMode(apple2, text: false, hiRes: false, mixed: true);

        // White blocks in the last graphics row and in the first text row's cell.
        apple2.Mem[Apple2TextScreen.GetAddress(Apple2Config.MixedModeFirstTextRow - 1, 0)] = 0xFF;
        apple2.Mem[Apple2TextScreen.GetAddress(Apple2Config.MixedModeFirstTextRow, 0)] = 0xFF;

        rasterizer.OnEndFrame();

        Assert.NotEqual(0u, PixelAt(rasterizer, 0, Apple2Config.MixedModeGraphicsHeight - 1));
        // $FF in the text area is a flashing '?' cell, not two white blocks; assert it is not
        // rendered as the solid lo-res white the graphics area shows.
        var white = Packed(Apple2LoResScreen.Palette[15]);
        Assert.NotEqual(white, PixelAt(rasterizer, 0, Apple2Config.MixedModeGraphicsHeight));
    }

    [Fact]
    public void Text_Mode_Ignores_Graphics_Memory()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectMode(apple2, text: true);
        ClearTextScreen(apple2);

        apple2.Mem[Apple2HiResScreen.HiResPage1BaseAddress] = 0x7F;
        rasterizer.OnEndFrame();

        for (var x = 0; x < Apple2HiResScreen.PixelsPerByte; x++)
            Assert.Equal(0u, PixelAt(rasterizer, x, 0));
    }
}
