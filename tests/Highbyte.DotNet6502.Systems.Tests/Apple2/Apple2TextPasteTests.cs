using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Tests.Apple2.TestRom;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Tests the text-paste path end to end: pasted characters are fed through the keyboard latch,
/// paced by strobe consumption, and consumed by ROM code (the synthetic ROM's keyboard echo
/// loop, which writes each read key to screen row <see cref="Apple2SyntheticRom.EchoRow"/>).
/// </summary>
public class Apple2TextPasteTests
{
    private static Apple2System BootSyntheticRom()
    {
        var romData = new Dictionary<string, byte[]>
        {
            { Apple2SystemConfig.SYSTEM_ROM_NAME, Apple2SyntheticRom.Build() },
        };
        return new Apple2System(new Apple2Config(), NullLoggerFactory.Instance, romData);
    }

    private static string EchoedText(Apple2System apple2, int length)
    {
        var text = "";
        for (var i = 0; i < length; i++)
        {
            var screenByte = apple2.Mem[Apple2TextScreen.GetAddress(
                Apple2SyntheticRom.EchoRow, Apple2SyntheticRom.EchoFirstColumn + i)];
            text += Apple2CharSet.ScreenCodeToUnicode(screenByte);
        }
        return text;
    }

    [Fact]
    public void Pasted_Text_Is_Typed_Into_The_Machine()
    {
        var apple2 = BootSyntheticRom();
        apple2.ExecuteOneFrame();   // boot: clear + banner, then the keyboard poll loop

        apple2.TextPaste.Paste("HB");
        for (var frame = 0; frame < 6; frame++)
            apple2.ExecuteOneFrame();

        Assert.Equal("HB", EchoedText(apple2, 2));
        Assert.False(apple2.Keyboard.StrobeSet);
    }

    [Fact]
    public void Lowercase_Letters_Are_Typed_As_Uppercase()
    {
        var apple2 = BootSyntheticRom();
        apple2.ExecuteOneFrame();

        apple2.TextPaste.Paste("hb");
        for (var frame = 0; frame < 6; frame++)
            apple2.ExecuteOneFrame();

        Assert.Equal("HB", EchoedText(apple2, 2));
    }

    [Fact]
    public void Characters_Are_Delivered_At_Most_One_Per_Frame()
    {
        var apple2 = BootSyntheticRom();
        apple2.ExecuteOneFrame();

        apple2.TextPaste.Paste("HB");

        // The first character is latched at the end of this frame; nothing is echoed yet.
        apple2.ExecuteOneFrame();
        Assert.Equal(" ", EchoedText(apple2, 1));   // still the cleared-screen space

        // The next frame's CPU run consumes and echoes 'H'; 'B' is latched at its end.
        apple2.ExecuteOneFrame();
        Assert.Equal("H ", EchoedText(apple2, 2));

        apple2.ExecuteOneFrame();
        Assert.Equal("HB", EchoedText(apple2, 2));
    }

    [Fact]
    public void Characters_The_Keyboard_Cannot_Produce_Are_Dropped()
    {
        var apple2 = BootSyntheticRom();
        apple2.ExecuteOneFrame();

        // '~' (and lowercase-range symbols in general) cannot be produced; 'H'/'B' can.
        apple2.TextPaste.Paste("H~B");
        for (var frame = 0; frame < 8; frame++)
            apple2.ExecuteOneFrame();

        Assert.Equal("HB", EchoedText(apple2, 2));
    }
}
