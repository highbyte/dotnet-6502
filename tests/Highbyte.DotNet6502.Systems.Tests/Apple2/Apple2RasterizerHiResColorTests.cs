using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Hi-res NTSC artifact colour, as decoded by <see cref="Apple2MonitorColor.Color"/>. The
/// monochrome dot mapping the phosphor monitors use is covered by
/// <see cref="Apple2RasterizerGraphicsTests"/>.
/// </summary>
public class Apple2RasterizerHiResColorTests
{
    private static Apple2System BuildApple2(Apple2MonitorColor monitorColor = Apple2MonitorColor.Color)
    {
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

    private static uint PixelAt(Apple2Rasterizer rasterizer, int x, int y = 0)
        => rasterizer.CurrentFrontLayerBuffers[1].Span[(y * rasterizer.NativeSize.Width) + x];

    private static uint Packed(System.Drawing.Color color)
        => Apple2Rasterizer.PackBgra(color.B, color.G, color.R, color.A);

    private static void SelectHiRes(Apple2System apple2, bool mixed = false)
    {
        _ = apple2.Mem[Apple2SoftSwitches.GraphicsModeAddress];
        _ = apple2.Mem[Apple2SoftSwitches.HiResModeAddress];
        _ = apple2.Mem[mixed ? Apple2SoftSwitches.MixedModeOnAddress : Apple2SoftSwitches.MixedModeOffAddress];
        _ = apple2.Mem[Apple2SoftSwitches.TextPage1Address];
    }

    /// <summary>Writes bytes to the start of hi-res line 0 and renders one frame.</summary>
    private static Apple2Rasterizer RenderLine(Apple2System apple2, params byte[] lineBytes)
    {
        var rasterizer = GetRasterizer(apple2);
        SelectHiRes(apple2);
        for (var i = 0; i < lineBytes.Length; i++)
            apple2.Mem[(ushort)(Apple2HiResScreen.HiResPage1BaseAddress + i)] = lineBytes[i];
        rasterizer.OnEndFrame();
        return rasterizer;
    }

    [Fact]
    public void Isolated_Dots_On_Even_Columns_Are_Violet()
    {
        var apple2 = BuildApple2();
        // $55 lights bits 0,2,4,6 -> columns 0,2,4,6, each with unlit neighbours.
        var rasterizer = RenderLine(apple2, 0x55);

        var violet = Packed(Apple2HiResColors.Violet);
        // Each lit dot tints its whole colour cycle, so columns 0-7 are a continuous violet run
        // rather than violet dots separated by black.
        for (var x = 0; x <= 7; x++)
            Assert.Equal(violet, PixelAt(rasterizer, x));
    }

    [Fact]
    public void Isolated_Dots_On_Odd_Columns_Are_Green()
    {
        var apple2 = BuildApple2();
        // $2A lights bits 1,3,5 -> columns 1,3,5, tinting the cycles (0,1), (2,3) and (4,5).
        var rasterizer = RenderLine(apple2, 0x2A);

        var green = Packed(Apple2HiResColors.Green);
        for (var x = 0; x <= 5; x++)
            Assert.Equal(green, PixelAt(rasterizer, x));
        // Nothing is lit in the cycle after it.
        Assert.Equal(0u, PixelAt(rasterizer, 6));
        Assert.Equal(0u, PixelAt(rasterizer, 7));
    }

    /// <summary>
    /// The unit of colour is the two-dot colour cycle, not the dot — a monitor cannot resolve the
    /// dots within a cycle. A single lit dot therefore paints two columns.
    /// </summary>
    [Fact]
    public void A_Single_Lit_Dot_Fills_Both_Columns_Of_Its_Color_Cycle()
    {
        var apple2 = BuildApple2();
        var rasterizer = RenderLine(apple2, 0x01);   // column 0 only

        var violet = Packed(Apple2HiResColors.Violet);
        Assert.Equal(violet, PixelAt(rasterizer, 0));
        Assert.Equal(violet, PixelAt(rasterizer, 1));
        Assert.Equal(0u, PixelAt(rasterizer, 2));
    }

    [Fact]
    public void A_Single_Lit_Dot_On_An_Odd_Column_Fills_Its_Cycle_Backwards()
    {
        var apple2 = BuildApple2();
        var rasterizer = RenderLine(apple2, 0x02);   // column 1 only

        var green = Packed(Apple2HiResColors.Green);
        // Column 1 belongs to the cycle (0,1), so the tint reaches back to column 0.
        Assert.Equal(green, PixelAt(rasterizer, 0));
        Assert.Equal(green, PixelAt(rasterizer, 1));
        Assert.Equal(0u, PixelAt(rasterizer, 2));
    }

    [Fact]
    public void Bit_7_Shifts_Violet_To_Blue()
    {
        var apple2 = BuildApple2();
        var rasterizer = RenderLine(apple2, 0xD5);   // $55 with the colour-shift bit set

        var blue = Packed(Apple2HiResColors.Blue);
        for (var x = 0; x <= 7; x++)
            Assert.Equal(blue, PixelAt(rasterizer, x));
    }

    [Fact]
    public void Bit_7_Shifts_Green_To_Orange()
    {
        var apple2 = BuildApple2();
        var rasterizer = RenderLine(apple2, 0xAA);   // $2A with the colour-shift bit set

        var orange = Packed(Apple2HiResColors.Orange);
        for (var x = 0; x <= 5; x++)
            Assert.Equal(orange, PixelAt(rasterizer, x));
    }

    [Fact]
    public void Bit_7_Alone_Still_Lights_Nothing()
    {
        var apple2 = BuildApple2();
        var rasterizer = RenderLine(apple2, 0x80);

        for (var x = 0; x < Apple2HiResScreen.PixelsPerByte; x++)
            Assert.Equal(0u, PixelAt(rasterizer, x));
    }

    [Fact]
    public void Adjacent_Dots_Read_As_White()
    {
        var apple2 = BuildApple2();
        var rasterizer = RenderLine(apple2, 0x03);   // columns 0 and 1

        var white = Packed(Apple2HiResColors.White);
        Assert.Equal(white, PixelAt(rasterizer, 0));
        Assert.Equal(white, PixelAt(rasterizer, 1));
        Assert.Equal(0u, PixelAt(rasterizer, 2));
    }

    /// <summary>
    /// A two-dot run straddling a cycle boundary stays two columns wide. White is decided per dot,
    /// so it must not spread to all four columns of the two cycles the run touches.
    /// </summary>
    [Fact]
    public void A_White_Run_Across_A_Cycle_Boundary_Does_Not_Widen()
    {
        var apple2 = BuildApple2();
        var rasterizer = RenderLine(apple2, 0x06);   // columns 1 and 2

        var white = Packed(Apple2HiResColors.White);
        Assert.Equal(0u, PixelAt(rasterizer, 0));
        Assert.Equal(white, PixelAt(rasterizer, 1));
        Assert.Equal(white, PixelAt(rasterizer, 2));
        Assert.Equal(0u, PixelAt(rasterizer, 3));
    }

    [Fact]
    public void A_Run_Of_Dots_Is_White_All_The_Way_Through()
    {
        var apple2 = BuildApple2();
        var rasterizer = RenderLine(apple2, 0x7F);   // all 7 pixels lit

        var white = Packed(Apple2HiResColors.White);
        for (var x = 0; x < Apple2HiResScreen.PixelsPerByte; x++)
            Assert.Equal(white, PixelAt(rasterizer, x));
    }

    [Fact]
    public void Dots_Adjacent_Across_A_Byte_Boundary_Read_As_White()
    {
        var apple2 = BuildApple2();
        // Last pixel of byte 0 (column 6) and first of byte 1 (column 7).
        var rasterizer = RenderLine(apple2, 0x40, 0x01);

        var white = Packed(Apple2HiResColors.White);
        Assert.Equal(white, PixelAt(rasterizer, 6));
        Assert.Equal(white, PixelAt(rasterizer, 7));
    }

    /// <summary>
    /// A byte carries 7 pixels, so the same bit position lands on the opposite column parity in
    /// the next byte. Two identical bytes therefore produce violet followed by green — which is
    /// why a solid violet line is written as alternating $55/$2A.
    /// </summary>
    [Fact]
    public void Column_Parity_Continues_Across_Bytes_Rather_Than_Restarting()
    {
        var apple2 = BuildApple2();
        // $15 lights bits 0,2,4 and leaves bit 6 clear, so column 6 stays unlit and the first dot
        // of the next byte (column 7) has no lit neighbour to turn it white.
        var rasterizer = RenderLine(apple2, 0x15, 0x15);

        var violet = Packed(Apple2HiResColors.Violet);
        var green = Packed(Apple2HiResColors.Green);
        Assert.Equal(violet, PixelAt(rasterizer, 0));
        // Byte 1 bit 0 is column 7 — odd, so green despite being the same byte value.
        Assert.Equal(green, PixelAt(rasterizer, 7));
    }

    [Fact]
    public void Alternating_55_2A_Bytes_Give_A_Continuous_Violet_Line()
    {
        var apple2 = BuildApple2();
        var rasterizer = RenderLine(apple2, 0x55, 0x2A, 0x55, 0x2A);

        var violet = Packed(Apple2HiResColors.Violet);
        // Every lit dot in the first four bytes lands on an even column, so every colour cycle is
        // tinted and the run is unbroken — no black gaps between the dots.
        for (var x = 0; x < 4 * Apple2HiResScreen.PixelsPerByte; x++)
            Assert.Equal(violet, PixelAt(rasterizer, x));
    }

    [Fact]
    public void The_Last_Column_Of_The_Line_Renders_Without_A_Right_Neighbour()
    {
        var apple2 = BuildApple2();
        var lastByte = new byte[Apple2HiResScreen.BytesPerLine];
        lastByte[^1] = 0x40;   // bit 6 of the final byte == column 279
        var rasterizer = RenderLine(apple2, lastByte);

        var lastColumn = Apple2Config.DrawableAreaWidth - 1;
        var green = Packed(Apple2HiResColors.Green);
        Assert.Equal(green, PixelAt(rasterizer, lastColumn));       // 279 is odd
        Assert.Equal(green, PixelAt(rasterizer, lastColumn - 1));   // its cycle partner
    }

    [Fact]
    public void Unlit_Dots_Leave_The_Foreground_Transparent_Over_A_Black_Background()
    {
        var apple2 = BuildApple2();
        var rasterizer = RenderLine(apple2, 0x00);

        Assert.Equal(0u, PixelAt(rasterizer, 0));
        Assert.Equal(Packed(Apple2Colors.Background), rasterizer.CurrentFrontLayerBuffers[0].Span[0]);
    }

    [Fact]
    public void Mixed_Mode_Still_Stops_The_Graphics_At_The_Text_Area()
    {
        var apple2 = BuildApple2();
        var rasterizer = GetRasterizer(apple2);
        SelectHiRes(apple2, mixed: true);

        apple2.Mem[Apple2HiResScreen.GetLineStartAddress(Apple2Config.MixedModeGraphicsHeight - 1)] = 0x55;
        apple2.Mem[Apple2HiResScreen.GetLineStartAddress(Apple2Config.MixedModeGraphicsHeight)] = 0x55;
        rasterizer.OnEndFrame();

        var violet = Packed(Apple2HiResColors.Violet);
        Assert.Equal(violet, PixelAt(rasterizer, 0, Apple2Config.MixedModeGraphicsHeight - 1));
        Assert.NotEqual(violet, PixelAt(rasterizer, 0, Apple2Config.MixedModeGraphicsHeight));
    }

    [Theory]
    [InlineData(Apple2MonitorColor.Green)]
    [InlineData(Apple2MonitorColor.White)]
    [InlineData(Apple2MonitorColor.Amber)]
    public void Phosphor_Monitors_Keep_Rendering_The_Raw_Dot_Pattern(Apple2MonitorColor monitorColor)
    {
        var apple2 = BuildApple2(monitorColor);
        var rasterizer = RenderLine(apple2, 0x55);

        var foreground = Packed(Apple2Colors.GetForeground(monitorColor));
        Assert.Equal(foreground, PixelAt(rasterizer, 0));
        Assert.NotEqual(Packed(Apple2HiResColors.Violet), PixelAt(rasterizer, 0));
    }

    [Fact]
    public void A_Color_Monitor_Renders_Text_White()
    {
        Assert.Equal(System.Drawing.Color.FromArgb(255, 255, 255, 255),
            Apple2Colors.GetForeground(Apple2MonitorColor.Color));
        Assert.True(Apple2Colors.IsColorMonitor(Apple2MonitorColor.Color));
        Assert.False(Apple2Colors.IsColorMonitor(Apple2MonitorColor.Green));
    }

    [Fact]
    public void The_Artifact_Colors_Come_From_The_LoRes_Palette()
    {
        // Hi-res artifact colours are the same signals lo-res generates directly.
        Assert.Equal(Apple2LoResScreen.Palette[0], Apple2HiResColors.Black);
        Assert.Equal(Apple2LoResScreen.Palette[3], Apple2HiResColors.Violet);
        Assert.Equal(Apple2LoResScreen.Palette[6], Apple2HiResColors.Blue);
        Assert.Equal(Apple2LoResScreen.Palette[9], Apple2HiResColors.Orange);
        Assert.Equal(Apple2LoResScreen.Palette[12], Apple2HiResColors.Green);
        Assert.Equal(Apple2LoResScreen.Palette[15], Apple2HiResColors.White);
    }
}
