using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2RasterizerTests
{
    /// <summary>
    /// A stand-in character generator with the same layout as the real 341-0036 dump: 64 glyphs
    /// of 8 scan lines, 5 dots per line in bits 5-1, most significant leftmost. Glyph <c>n</c>
    /// gets a pattern derived from its index so tests can tell glyphs apart.
    /// </summary>
    private static byte[] BuildTestCharacterRom()
    {
        var spaceGlyph = Apple2CharSet.GetGlyphIndex(Apple2CharSet.FromAscii((byte)' '));

        var rom = new byte[Apple2CharSet.CharacterRomSize];
        for (var glyph = 0; glyph < Apple2CharSet.GlyphCount; glyph++)
        {
            if (glyph == spaceGlyph)
                continue;   // blank, like the real character generator

            for (var row = 0; row < Apple2CharSet.GlyphRowCount; row++)
                rom[(glyph * Apple2CharSet.GlyphRowCount) + row] = (byte)((((glyph + row) & 0x1F) | 0x01) << 1);
        }
        return rom;
    }

    /// <summary>
    /// Fills the text page with normal-video spaces. Unwritten RAM reads as $00, which is an
    /// <em>inverse</em> '@' — a lit cell — so tests that assert blankness must clear first.
    /// </summary>
    private static void ClearScreen(Apple2System apple2, ushort pageBaseAddress = Apple2TextScreen.TextPage1BaseAddress)
    {
        var space = Apple2CharSet.FromAscii((byte)' ');
        for (var row = 0; row < Apple2Config.Rows; row++)
            for (var col = 0; col < Apple2Config.Cols; col++)
                apple2.Mem[Apple2TextScreen.GetAddress(row, col, pageBaseAddress)] = space;
    }

    private static Apple2System BuildApple2(byte[]? characterRom = null)
    {
        var romData = new Dictionary<string, byte[]>
        {
            { Apple2SystemConfig.CHARGEN_ROM_NAME, characterRom ?? BuildTestCharacterRom() },
        };
        // Pinned to a phosphor monitor: these tests assert green text, independently of whichever
        // monitor the shipped config defaults to.
        return new Apple2System(
            new Apple2Config { MonitorColor = Apple2MonitorColor.Green },
            NullLoggerFactory.Instance,
            romData);
    }

    private static Apple2Rasterizer GetRasterizer(Apple2System apple2)
        => (Apple2Rasterizer)apple2.RenderProvider!;

    private static uint PixelAt(Apple2Rasterizer rasterizer, int x, int y)
        => rasterizer.CurrentFrontLayerBuffers[1].Span[(y * rasterizer.NativeSize.Width) + x];

    private static uint BackgroundPixelAt(Apple2Rasterizer rasterizer, int x, int y)
        => rasterizer.CurrentFrontLayerBuffers[0].Span[(y * rasterizer.NativeSize.Width) + x];

    private static uint Foreground(Apple2MonitorColor color = Apple2MonitorColor.Green)
    {
        var c = Apple2Colors.GetForeground(color);
        return Apple2Rasterizer.PackBgra(c.B, c.G, c.R, c.A);
    }

    [Fact]
    public void The_Rasterizer_Is_The_Default_Render_Provider()
    {
        Assert.IsType<Apple2Rasterizer>(BuildApple2().RenderProvider);
    }

    [Fact]
    public void The_Native_Size_Is_The_Hardware_280_By_192()
    {
        var rasterizer = GetRasterizer(BuildApple2());

        Assert.Equal(280, rasterizer.NativeSize.Width);
        Assert.Equal(192, rasterizer.NativeSize.Height);
        Assert.Equal(280 * 4, rasterizer.StrideBytes);
        Assert.Equal(7, Apple2Config.CharacterWidth);
    }

    [Fact]
    public void A_Blank_Screen_Draws_Only_Background()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);

        ClearScreen(apple2);

        rasterizer.OnEndFrame();

        Assert.All(rasterizer.CurrentFrontLayerBuffers[1].ToArray(), p => Assert.Equal(0u, p));
    }

    [Fact]
    public void A_Glyph_Is_Drawn_From_The_Character_Generator_Dot_Pattern()
    {
        var characterRom = BuildTestCharacterRom();
        var apple2 = BuildApple2(characterRom);
        var rasterizer = GetRasterizer(apple2);

        var screenByte = Apple2CharSet.FromAscii((byte)'A');
        var glyphIndex = Apple2CharSet.GetGlyphIndex(screenByte);
        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = screenByte;

        rasterizer.OnEndFrame();

        var foreground = Foreground();
        for (var glyphRow = 0; glyphRow < Apple2CharSet.GlyphRowCount; glyphRow++)
        {
            var line = characterRom[(glyphIndex * Apple2CharSet.GlyphRowCount) + glyphRow];
            for (var dot = 0; dot < Apple2Config.CharacterWidth; dot++)
            {
                var expectedLit = dot < Apple2CharSet.GlyphDotWidth
                                  && ((line >> (Apple2CharSet.GlyphDotShift - dot)) & 1) != 0;
                var actual = PixelAt(rasterizer, dot, glyphRow);
                Assert.Equal(expectedLit ? foreground : 0u, actual);
            }
        }
    }

    [Fact]
    public void The_Last_Two_Columns_Of_A_Cell_Are_Always_The_Inter_Character_Gap()
    {
        // A character generator whose stored dots are all set still leaves the 2 gap columns unlit.
        var allDotsRom = new byte[Apple2CharSet.CharacterRomSize];
        Array.Fill(allDotsRom, (byte)0x3E);   // bits 5-1 set
        var apple2 = BuildApple2(allDotsRom);
        var rasterizer = GetRasterizer(apple2);

        ClearScreen(apple2);
        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = Apple2CharSet.FromAscii((byte)'A');
        rasterizer.OnEndFrame();

        for (var dot = 0; dot < Apple2CharSet.GlyphDotWidth; dot++)
            Assert.NotEqual(0u, PixelAt(rasterizer, dot, 0));

        Assert.Equal(0u, PixelAt(rasterizer, 5, 0));
        Assert.Equal(0u, PixelAt(rasterizer, 6, 0));
    }

    [Fact]
    public void Interleaved_Rows_Land_On_The_Right_Scan_Lines()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        ClearScreen(apple2);

        // Row 8 lives at $0428 — the second band, not 8 * 40 bytes into the page.
        apple2.Mem[Apple2TextScreen.GetRowStartAddress(8)] = Apple2CharSet.FromAscii((byte)'A');
        rasterizer.OnEndFrame();

        var rowHasPixels = new bool[Apple2Config.Rows];
        for (var row = 0; row < Apple2Config.Rows; row++)
        {
            for (var line = 0; line < Apple2Config.CharacterHeight && !rowHasPixels[row]; line++)
                for (var x = 0; x < Apple2Config.CharacterWidth; x++)
                    if (PixelAt(rasterizer, x, (row * Apple2Config.CharacterHeight) + line) != 0u)
                    {
                        rowHasPixels[row] = true;
                        break;
                    }
        }

        Assert.True(rowHasPixels[8]);
        for (var row = 0; row < Apple2Config.Rows; row++)
            if (row != 8)
                Assert.False(rowHasPixels[row], $"Row {row} should be blank.");
    }

    [Fact]
    public void Inverse_Video_Lights_The_Dots_The_Glyph_Leaves_Dark()
    {
        var characterRom = BuildTestCharacterRom();
        var apple2 = BuildApple2(characterRom);
        var rasterizer = GetRasterizer(apple2);

        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = Apple2CharSet.FromAscii((byte)'A', Apple2TextAttribute.Normal);
        rasterizer.OnEndFrame();
        var normal = Enumerable.Range(0, Apple2Config.CharacterWidth).Select(x => PixelAt(rasterizer, x, 1)).ToArray();

        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = Apple2CharSet.FromAscii((byte)'A', Apple2TextAttribute.Inverse);
        rasterizer.OnEndFrame();
        var inverse = Enumerable.Range(0, Apple2Config.CharacterWidth).Select(x => PixelAt(rasterizer, x, 1)).ToArray();

        for (var x = 0; x < Apple2Config.CharacterWidth; x++)
            Assert.NotEqual(normal[x] != 0u, inverse[x] != 0u);
    }

    [Fact]
    public void Flashing_Characters_Alternate_Between_Normal_And_Inverse()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = Apple2CharSet.FromAscii((byte)'A', Apple2TextAttribute.Flash);

        var phases = new List<bool>();
        for (var frame = 0; frame < Apple2Config.FlashFramesPerToggle * 4; frame++)
        {
            rasterizer.OnEndFrame();
            phases.Add(rasterizer.FlashPhaseInverted);
        }

        Assert.Contains(true, phases);
        Assert.Contains(false, phases);
        Assert.Equal(phases.Count / 2, phases.Count(inverted => inverted));
    }

    [Fact]
    public void The_Monitor_Colour_Selects_The_Foreground()
    {
        var apple2 = new Apple2System(
            new Apple2Config { MonitorColor = Apple2MonitorColor.Amber },
            NullLoggerFactory.Instance,
            new Dictionary<string, byte[]> { { Apple2SystemConfig.CHARGEN_ROM_NAME, BuildTestCharacterRom() } });
        var rasterizer = GetRasterizer(apple2);

        ClearScreen(apple2);
        // Inverse space lights the whole cell.
        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = Apple2CharSet.FromAscii((byte)' ', Apple2TextAttribute.Inverse);
        rasterizer.OnEndFrame();

        Assert.Equal(Foreground(Apple2MonitorColor.Amber), PixelAt(rasterizer, 0, 0));
        Assert.Equal(Apple2Rasterizer.PackBgra(Apple2Colors.Background.B, Apple2Colors.Background.G,
            Apple2Colors.Background.R, Apple2Colors.Background.A), BackgroundPixelAt(rasterizer, 0, 0));
    }

    [Fact]
    public void The_Page2_Soft_Switch_Selects_The_Second_Text_Page()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        ClearScreen(apple2, Apple2TextScreen.TextPage1BaseAddress);
        ClearScreen(apple2, Apple2TextScreen.TextPage2BaseAddress);

        apple2.Mem[Apple2TextScreen.GetAddress(0, 0, Apple2TextScreen.TextPage2BaseAddress)] =
            Apple2CharSet.FromAscii((byte)' ', Apple2TextAttribute.Inverse);

        rasterizer.OnEndFrame();
        Assert.Equal(0u, PixelAt(rasterizer, 0, 0));   // page 1 is still blank

        _ = apple2.Mem[Apple2SoftSwitches.TextPage2Address];
        rasterizer.OnEndFrame();
        Assert.NotEqual(0u, PixelAt(rasterizer, 0, 0));
    }

    [Fact]
    public void Without_A_Character_Generator_The_Screen_Is_Blank_Rather_Than_Garbage()
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);
        var rasterizer = GetRasterizer(apple2);

        Assert.Null(apple2.CharacterRom);

        ClearScreen(apple2);
        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = Apple2CharSet.FromAscii((byte)'A');
        rasterizer.OnEndFrame();

        Assert.All(rasterizer.CurrentFrontLayerBuffers[1].ToArray(), p => Assert.Equal(0u, p));
    }

    [Fact]
    public void ExtractCharacterRomImage_Takes_The_Unique_Leading_Glyph_Block()
    {
        // The real dump is 2 KB: 64 glyphs, then the same set with bit 7 set, then both again.
        var unique = BuildTestCharacterRom();
        var padded = new byte[2048];
        for (var i = 0; i < 512; i++)
        {
            padded[i] = unique[i];
            padded[512 + i] = (byte)(unique[i] | 0x80);
            padded[1024 + i] = unique[i];
            padded[1536 + i] = (byte)(unique[i] | 0x80);
        }

        Assert.Equal(unique, Apple2System.ExtractCharacterRomImage(padded));
        Assert.Same(unique, Apple2System.ExtractCharacterRomImage(unique));
    }

    [Fact]
    public void ExtractCharacterRomImage_Rejects_An_Undersized_Image()
    {
        Assert.Throws<DotNet6502Exception>(() => Apple2System.ExtractCharacterRomImage(new byte[256]));
    }
}
