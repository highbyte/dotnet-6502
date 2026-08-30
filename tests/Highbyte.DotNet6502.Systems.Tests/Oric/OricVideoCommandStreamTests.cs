using Highbyte.DotNet6502.Impl.Terminal;
using Highbyte.DotNet6502.Systems.Oric.Render;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricVideoCommandStreamTests
{
    [Fact]
    public void Renders_The_Oric_Text_Screen_As_Unicode_Cells()
    {
        var oric = new OricMachine();
        var stream = Assert.IsType<OricVideoCommandStream>(
            oric.RenderProviders.Single(provider => provider is OricVideoCommandStream));
        var target = new TerminalRenderTarget();
        oric.Mem[OricRasterizer.TextScreenAddress] = (byte)'A';
        oric.Mem[(ushort)(OricRasterizer.TextScreenAddress + 1)] = (byte)(0x80 | 'B');

        var buffer = Render(stream, target);

        Assert.Equal('A', buffer[0, 0].Rune.Value);
        Assert.Equal('B', buffer[0, 1].Rune.Value);
        Assert.Equal(buffer[0, 0].Foreground, buffer[0, 1].Background);
        Assert.Equal(buffer[0, 0].Background, buffer[0, 1].Foreground);
    }

    [Fact]
    public void Applies_Serial_Ink_And_Paper_Attributes_To_Following_Cells()
    {
        var oric = new OricMachine();
        var stream = Assert.IsType<OricVideoCommandStream>(
            oric.RenderProviders.Single(provider => provider is OricVideoCommandStream));
        var target = new TerminalRenderTarget();
        oric.Mem[OricRasterizer.TextScreenAddress] = 0x01; // red ink
        oric.Mem[(ushort)(OricRasterizer.TextScreenAddress + 1)] = 0x14; // blue paper
        oric.Mem[(ushort)(OricRasterizer.TextScreenAddress + 2)] = (byte)'C';

        var buffer = Render(stream, target);

        Assert.Equal(' ', buffer[0, 0].Rune.Value);
        Assert.Equal(' ', buffer[0, 1].Rune.Value);
        Assert.Equal('C', buffer[0, 2].Rune.Value);
        Assert.Equal((255, 0, 0), ToRgb(buffer[0, 2].Foreground));
        Assert.Equal((0, 0, 255), ToRgb(buffer[0, 2].Background));
    }

    [Theory]
    [InlineData(0x00, ' ')]
    [InlineData(0x41, 'A')]
    [InlineData(0xc1, 'A')]
    [InlineData(0x60, '©')]
    [InlineData(0x7f, '█')]
    public void Converts_Oric_Screen_Codes_To_Unicode(byte screenCode, char expected)
        => Assert.Equal(expected.ToString(), OricVideoCommandStream.ScreenCodeToUnicode(screenCode));

    private static TerminalRenderTarget.ScreenCell[,] Render(
        OricVideoCommandStream stream,
        TerminalRenderTarget target)
    {
        stream.OnEndFrame();
        target.BeginFrame();
        foreach (var command in stream.DequeueAll())
            target.Execute(command);
        target.EndFrame();

        var buffer = new TerminalRenderTarget.ScreenCell[1, 1];
        _ = target.Snapshot(ref buffer);
        return buffer;
    }

    private static (int R, int G, int B) ToRgb(Terminal.Gui.Drawing.Color color)
        => (color.R, color.G, color.B);
}
