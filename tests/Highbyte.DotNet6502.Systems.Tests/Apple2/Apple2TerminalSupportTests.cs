using Highbyte.DotNet6502.Impl.Terminal;
using Highbyte.DotNet6502.Impl.Terminal.Apple2;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2TerminalSupportTests
{
    [Fact]
    public void Terminal_Config_Selects_Text_Rendering_And_Disables_Audio()
    {
        var config = new Apple2TerminalHostConfig();

        Assert.False(config.AudioSupported);
        Assert.False(config.SystemConfig.AudioEnabled);
        Assert.Equal(typeof(Apple2VideoCommandStream), config.SystemConfig.RenderProviderType);
    }

    [Fact]
    public async Task Terminal_Setup_Wires_Apple2_Keyboard_And_Joystick_Input()
    {
        var setup = new Apple2TerminalSetup(
            NullLoggerFactory.Instance,
            new ConfigurationBuilder().Build());
        var hostConfig = new Apple2TerminalHostConfig();
        hostConfig.SystemConfig.KeyboardJoystickEnabled = true;
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);

        _ = await setup.BuildSystemRunner(apple2, hostConfig);

        var input = Assert.IsType<Apple2InputHandler>(apple2.InputConsumer);
        Assert.True(input.InputConfig.KeyboardJoystickEnabled);
    }

    [Fact]
    public void Apple2_Normal_And_Inverse_Attributes_Reach_The_Terminal_Unchanged()
    {
        var apple2 = new Apple2System(
            new Apple2Config { MonitorColor = Apple2MonitorColor.Green },
            NullLoggerFactory.Instance);
        apple2.SetCurrentRenderProviderType(typeof(Apple2VideoCommandStream));
        var stream = Assert.IsType<Apple2VideoCommandStream>(apple2.RenderProvider);
        var target = new TerminalRenderTarget();

        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] =
            Apple2CharSet.FromAscii((byte)'A', Apple2TextAttribute.Normal);
        var normal = RenderCell(stream, target);

        apple2.Mem[Apple2TextScreen.GetAddress(0, 0)] =
            Apple2CharSet.FromAscii((byte)'A', Apple2TextAttribute.Inverse);
        var inverse = RenderCell(stream, target);

        Assert.Equal('A', normal.Rune.Value);
        Assert.Equal('A', inverse.Rune.Value);
        Assert.Equal(normal.Foreground, inverse.Background);
        Assert.Equal(normal.Background, inverse.Foreground);
    }

    [Fact]
    public void Commodore_High_Bit_Reverse_Video_Remains_Opt_In()
    {
        var target = new TerminalRenderTarget();
        var foreground = System.Drawing.Color.Red;
        var background = System.Drawing.Color.Blue;

        target.BeginFrame();
        target.Execute(new SetConfig(code => ((char)code).ToString()) { ReverseVideoHighBit = true });
        target.Execute(new DrawGlyph(0, 0, 0xC1, foreground, background));
        target.EndFrame();

        var buffer = new TerminalRenderTarget.ScreenCell[1, 1];
        _ = target.Snapshot(ref buffer);
        Assert.Equal('A', buffer[0, 0].Rune.Value);
        Assert.Equal(0, buffer[0, 0].Foreground.R);
        Assert.Equal(0, buffer[0, 0].Foreground.G);
        Assert.Equal(255, buffer[0, 0].Foreground.B);
        Assert.Equal(255, buffer[0, 0].Background.R);
        Assert.Equal(0, buffer[0, 0].Background.G);
        Assert.Equal(0, buffer[0, 0].Background.B);
    }

    private static TerminalRenderTarget.ScreenCell RenderCell(
        Apple2VideoCommandStream stream,
        TerminalRenderTarget target)
    {
        stream.OnEndFrame();
        target.BeginFrame();
        foreach (var command in stream.DequeueAll())
            target.Execute(command);
        target.EndFrame();

        var buffer = new TerminalRenderTarget.ScreenCell[1, 1];
        _ = target.Snapshot(ref buffer);
        return buffer[0, 0];
    }
}
