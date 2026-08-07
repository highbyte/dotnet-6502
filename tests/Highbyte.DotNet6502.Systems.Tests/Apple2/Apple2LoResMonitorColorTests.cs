using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Lo-res through the monitor. A monochrome monitor has no chroma to show, so the 16 lo-res
/// colors arrive as shades of its phosphor rather than as colors.
/// </summary>
public class Apple2LoResMonitorColorTests
{
    private static Apple2System BuildApple2(Apple2MonitorColor monitorColor)
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

    private static Apple2Rasterizer RenderLoResCell(Apple2System apple2, byte screenByte)
    {
        var rasterizer = (Apple2Rasterizer)apple2.RenderProvider!;
        _ = apple2.Mem[Apple2SoftSwitches.GraphicsModeAddress];
        _ = apple2.Mem[Apple2SoftSwitches.LoResModeAddress];
        _ = apple2.Mem[Apple2SoftSwitches.MixedModeOffAddress];
        _ = apple2.Mem[Apple2SoftSwitches.TextPage1Address];
        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] = screenByte;
        rasterizer.OnEndFrame();
        return rasterizer;
    }

    private static uint PixelAt(Apple2Rasterizer rasterizer, int x, int y)
        => rasterizer.CurrentFrontLayerBuffers[1].Span[(y * rasterizer.NativeSize.Width) + x];

    private static uint Packed(System.Drawing.Color color)
        => Apple2Rasterizer.PackBgra(color.B, color.G, color.R, color.A);

    [Fact]
    public void A_Color_Monitor_Shows_The_Full_LoRes_Palette()
    {
        var apple2 = BuildApple2(Apple2MonitorColor.Color);
        var rasterizer = RenderLoResCell(apple2, 0x01);   // upper block magenta (1)

        Assert.Equal(Packed(Apple2LoResScreen.Palette[1]), PixelAt(rasterizer, 0, 0));
    }

    [Theory]
    [InlineData(Apple2MonitorColor.Green)]
    [InlineData(Apple2MonitorColor.White)]
    [InlineData(Apple2MonitorColor.Amber)]
    public void A_Phosphor_Monitor_Shows_LoRes_As_Shades_Of_Its_Phosphor(Apple2MonitorColor monitorColor)
    {
        var apple2 = BuildApple2(monitorColor);
        var rasterizer = RenderLoResCell(apple2, 0x01);   // magenta on a colour monitor

        var rendered = PixelAt(rasterizer, 0, 0);
        Assert.NotEqual(Packed(Apple2LoResScreen.Palette[1]), rendered);
        Assert.Equal(Packed(Apple2Colors.ApplyMonitor(Apple2LoResScreen.Palette[1], monitorColor)), rendered);
    }

    /// <summary>
    /// The point of the luminance model: colours that differ only in hue must stay distinguishable
    /// by brightness, and a brighter colour must stay brighter.
    /// </summary>
    [Fact]
    public void Phosphor_Shades_Preserve_The_Brightness_Order_Of_The_Palette()
    {
        var black = Apple2Colors.ApplyMonitor(Apple2LoResScreen.Palette[0], Apple2MonitorColor.Green);
        var darkBlue = Apple2Colors.ApplyMonitor(Apple2LoResScreen.Palette[2], Apple2MonitorColor.Green);
        var grey = Apple2Colors.ApplyMonitor(Apple2LoResScreen.Palette[5], Apple2MonitorColor.Green);
        var white = Apple2Colors.ApplyMonitor(Apple2LoResScreen.Palette[15], Apple2MonitorColor.Green);

        Assert.True(black.G < darkBlue.G, "black must be darker than dark blue");
        Assert.True(darkBlue.G < grey.G, "dark blue must be darker than grey");
        Assert.True(grey.G < white.G, "grey must be darker than white");
    }

    [Fact]
    public void Black_Stays_Black_And_White_Reaches_Full_Phosphor()
    {
        Assert.Equal(
            System.Drawing.Color.FromArgb(255, 0, 0, 0),
            Apple2Colors.ApplyMonitor(Apple2LoResScreen.Palette[0], Apple2MonitorColor.Amber));

        // Palette entry 15 is pure white, so it maps to the phosphor at full intensity.
        Assert.Equal(
            Apple2Colors.GetForeground(Apple2MonitorColor.Amber),
            Apple2Colors.ApplyMonitor(Apple2LoResScreen.Palette[15], Apple2MonitorColor.Amber));
    }

    [Fact]
    public void A_Color_Monitor_Passes_The_Signal_Through_Unchanged()
    {
        foreach (var color in Apple2LoResScreen.Palette)
            Assert.Equal(color, Apple2Colors.ApplyMonitor(color, Apple2MonitorColor.Color));
    }

    /// <summary>Two colours with the same luminance are genuinely indistinguishable in mono —
    /// that is the hardware's behaviour, not a defect in the mapping.</summary>
    [Fact]
    public void The_Two_Identical_Greys_Stay_Identical()
    {
        Assert.Equal(
            Apple2Colors.ApplyMonitor(Apple2LoResScreen.Palette[5], Apple2MonitorColor.Green),
            Apple2Colors.ApplyMonitor(Apple2LoResScreen.Palette[10], Apple2MonitorColor.Green));
    }
}
