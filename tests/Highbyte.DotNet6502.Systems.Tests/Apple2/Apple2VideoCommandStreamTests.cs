using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2VideoCommandStreamTests
{
    private static Apple2System BuildApple2(Apple2MonitorColor monitorColor = Apple2MonitorColor.Green)
        => new(new Apple2Config { MonitorColor = monitorColor }, NullLoggerFactory.Instance);

    /// <summary>
    /// The rasterizer is the default provider, so select the command stream explicitly.
    /// </summary>
    private static Apple2VideoCommandStream GetCommandStream(Apple2System apple2)
    {
        apple2.SetCurrentRenderProviderType(typeof(Apple2VideoCommandStream));
        return (Apple2VideoCommandStream)apple2.RenderProvider!;
    }

    private static List<DrawGlyph> RenderOnce(Apple2System apple2)
    {
        var stream = GetCommandStream(apple2);
        stream.OnEndFrame();
        return stream.DequeueAll().OfType<DrawGlyph>().ToList();
    }

    [Fact]
    public void The_Command_Stream_Is_Offered_Alongside_The_Rasterizer()
    {
        var apple2 = BuildApple2();

        Assert.Contains(apple2.RenderProviders, p => p is Apple2VideoCommandStream);
        Assert.Contains(apple2.RenderProviders, p => p is Apple2Rasterizer);
    }

    [Fact]
    public void A_Frame_Emits_One_Glyph_Per_Character_Cell()
    {
        var apple2 = BuildApple2();

        var glyphs = RenderOnce(apple2);

        Assert.Equal(Apple2Config.Cols * Apple2Config.Rows, glyphs.Count);
        Assert.Equal(Apple2Config.Cols * Apple2Config.Rows, glyphs.Select(g => (g.X, g.Y)).Distinct().Count());
    }

    [Fact]
    public void Interleaved_Row_Addresses_Are_Drawn_At_Their_Screen_Row()
    {
        var apple2 = BuildApple2();

        // Distinct character in column 0 of every row, written through the interleaved address
        // of that row — proving the renderer resolves the same layout the hardware uses.
        for (var row = 0; row < Apple2Config.Rows; row++)
            apple2.Mem[Apple2TextScreen.GetRowStartAddress(row)] = (byte)(0xC1 + row);

        var glyphs = RenderOnce(apple2);

        for (var row = 0; row < Apple2Config.Rows; row++)
        {
            var glyph = glyphs.Single(g => g.X == 0 && g.Y == row);
            Assert.Equal(0xC1 + row, glyph.GlyphId);
        }
    }

    [Fact]
    public void Writing_To_A_Screen_Hole_Does_Not_Change_The_Display()
    {
        var apple2 = BuildApple2();
        var before = RenderOnce(apple2);

        // $0478-$047F is the screen hole of the first 128-byte block.
        for (ushort address = 0x0478; address <= 0x047F; address++)
            apple2.Mem[address] = 0xC1;

        var after = RenderOnce(apple2);

        Assert.Equal(before, after);
    }

    [Fact]
    public void Normal_Video_Draws_Foreground_On_Background()
    {
        var apple2 = BuildApple2(Apple2MonitorColor.Green);
        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = Apple2CharSet.FromAscii((byte)'A', Apple2TextAttribute.Normal);

        var glyph = RenderOnce(apple2).Single(g => g is { X: 0, Y: 0 });

        Assert.Equal(Apple2Colors.GetForeground(Apple2MonitorColor.Green), glyph.ForeColor);
        Assert.Equal(Apple2Colors.Background, glyph.BackColor);
    }

    [Fact]
    public void Inverse_Video_Swaps_Foreground_And_Background()
    {
        var apple2 = BuildApple2(Apple2MonitorColor.Green);
        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = Apple2CharSet.FromAscii((byte)'A', Apple2TextAttribute.Inverse);

        var glyph = RenderOnce(apple2).Single(g => g is { X: 0, Y: 0 });

        Assert.Equal(Apple2Colors.Background, glyph.ForeColor);
        Assert.Equal(Apple2Colors.GetForeground(Apple2MonitorColor.Green), glyph.BackColor);
    }

    [Fact]
    public void Flashing_Characters_Alternate_Between_Normal_And_Inverse()
    {
        var apple2 = BuildApple2();
        var stream = GetCommandStream(apple2);
        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = Apple2CharSet.FromAscii((byte)'A', Apple2TextAttribute.Flash);

        var phases = new List<bool>();
        // Two full flash periods.
        for (var frame = 0; frame < Apple2Config.FlashFramesPerToggle * 4; frame++)
        {
            stream.OnEndFrame();
            var glyph = stream.DequeueAll().OfType<DrawGlyph>().Single(g => g is { X: 0, Y: 0 });
            phases.Add(glyph.ForeColor == Apple2Colors.Background);   // true == currently inverted
        }

        Assert.Contains(true, phases);
        Assert.Contains(false, phases);
        // Roughly equal time in each phase — a ~2 Hz blink, not a per-frame flicker.
        Assert.Equal(phases.Count / 2, phases.Count(inverted => inverted));
    }

    [Fact]
    public void The_Page2_Soft_Switch_Selects_The_Second_Text_Page()
    {
        var apple2 = BuildApple2();
        apple2.Mem[Apple2TextScreen.GetAddress(5, 3, Apple2TextScreen.TextPage1BaseAddress)] = 0xC1;  // 'A'
        apple2.Mem[Apple2TextScreen.GetAddress(5, 3, Apple2TextScreen.TextPage2BaseAddress)] = 0xC2;  // 'B'

        Assert.Equal(0xC1, RenderOnce(apple2).Single(g => g is { X: 3, Y: 5 }).GlyphId);

        _ = apple2.Mem[Apple2SoftSwitches.TextPage2Address];

        Assert.Equal(0xC2, RenderOnce(apple2).Single(g => g is { X: 3, Y: 5 }).GlyphId);
    }

    [Fact]
    public void The_Frame_Starts_With_The_Glyph_To_Text_Converter_Configuration()
    {
        var apple2 = BuildApple2();
        var stream = GetCommandStream(apple2);

        stream.OnEndFrame();
        var commands = stream.DequeueAll().ToList();

        var setConfig = Assert.IsType<SetConfig>(commands.First());
        Assert.Equal("A", setConfig.GlyphToUnicodeConverter(0xC1));
    }

    [Fact]
    public void FrameCompleted_Is_Raised_Once_Per_Frame()
    {
        var apple2 = BuildApple2();
        var stream = GetCommandStream(apple2);
        var raised = 0;
        stream.FrameCompleted += (_, _) => raised++;

        stream.OnEndFrame();
        stream.OnEndFrame();

        Assert.Equal(2, raised);
    }
}
