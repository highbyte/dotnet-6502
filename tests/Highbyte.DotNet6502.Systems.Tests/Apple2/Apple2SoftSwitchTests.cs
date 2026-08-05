using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2SoftSwitchTests
{
    private static Apple2System BuildApple2() => new(new Apple2Config(), NullLoggerFactory.Instance);

    [Fact]
    public void Keyboard_Read_Returns_Zero_Before_Any_Key_Is_Pressed()
    {
        var apple2 = BuildApple2();
        Assert.Equal((byte)0x00, apple2.Mem[Apple2SoftSwitches.KeyboardDataAddress]);
    }

    [Fact]
    public void Key_Press_Latches_Ascii_With_Strobe_Set()
    {
        var apple2 = BuildApple2();
        apple2.Keyboard.KeyPressed((byte)'A');

        // $C000: ASCII in bits 6-0, strobe in bit 7.
        Assert.Equal((byte)0xC1, apple2.Mem[Apple2SoftSwitches.KeyboardDataAddress]);
        Assert.True(apple2.Keyboard.StrobeSet);
    }

    [Fact]
    public void Reading_KeyboardData_Does_Not_Clear_The_Strobe()
    {
        var apple2 = BuildApple2();
        apple2.Keyboard.KeyPressed((byte)'A');

        _ = apple2.Mem[Apple2SoftSwitches.KeyboardDataAddress];

        Assert.Equal((byte)0xC1, apple2.Mem[Apple2SoftSwitches.KeyboardDataAddress]);
    }

    [Fact]
    public void Accessing_StrobeClear_Clears_The_Strobe_But_Keeps_The_Ascii_Code()
    {
        var apple2 = BuildApple2();
        apple2.Keyboard.KeyPressed((byte)'A');

        var valueAtClear = apple2.Mem[Apple2SoftSwitches.KeyboardStrobeClearAddress];

        Assert.Equal((byte)0xC1, valueAtClear);                                            // pre-clear latch
        Assert.False(apple2.Keyboard.StrobeSet);
        Assert.Equal((byte)0x41, apple2.Mem[Apple2SoftSwitches.KeyboardDataAddress]);      // ASCII, strobe gone
    }

    [Fact]
    public void Writing_To_StrobeClear_Has_The_Same_Effect_As_Reading_It()
    {
        var apple2 = BuildApple2();
        apple2.Keyboard.KeyPressed((byte)'A');

        apple2.Mem[Apple2SoftSwitches.KeyboardStrobeClearAddress] = 0x00;

        Assert.False(apple2.Keyboard.StrobeSet);
    }

    [Theory]
    [InlineData(0xC000)]
    [InlineData(0xC00F)]
    public void Keyboard_Data_Is_Decoded_Across_Its_Whole_Sixteen_Address_Block(ushort address)
    {
        var apple2 = BuildApple2();
        apple2.Keyboard.KeyPressed((byte)'Z');

        Assert.Equal((byte)0xDA, apple2.Mem[address]);
    }

    [Theory]
    [InlineData(0xC010)]
    [InlineData(0xC01F)]
    public void Strobe_Clear_Is_Decoded_Across_Its_Whole_Sixteen_Address_Block(ushort address)
    {
        var apple2 = BuildApple2();
        apple2.Keyboard.KeyPressed((byte)'Z');

        _ = apple2.Mem[address];

        Assert.False(apple2.Keyboard.StrobeSet);
    }

    [Theory]
    [InlineData(0xC100)]
    [InlineData(0xC700)]
    [InlineData(0xCFFF)]
    public void Empty_Peripheral_Slots_Read_As_Unconnected(ushort address)
    {
        var apple2 = BuildApple2();

        // The Autostart ROM scans the slots for a disk controller signature; reading $FF makes
        // that scan fail so it falls through to BASIC instead of trying to boot.
        Assert.Equal(Apple2SoftSwitches.UnconnectedReadValue, apple2.Mem[address]);
    }

    [Fact]
    public void Writes_To_Empty_Peripheral_Slots_Are_Ignored()
    {
        var apple2 = BuildApple2();

        apple2.Mem[0xC700] = 0x42;

        Assert.Equal(Apple2SoftSwitches.UnconnectedReadValue, apple2.Mem[0xC700]);
    }

    [Fact]
    public void Rom_Socket_Reads_As_Unconnected_When_No_Rom_Is_Loaded()
    {
        var apple2 = BuildApple2();

        Assert.Equal(Apple2SoftSwitches.UnconnectedReadValue, apple2.Mem[0xD000]);
        Assert.Equal(Apple2SoftSwitches.UnconnectedReadValue, apple2.Mem[0xFFFF]);
    }

    [Fact]
    public void Ram_Covers_The_Whole_Forty_Eight_Kilobytes()
    {
        var apple2 = BuildApple2();

        apple2.Mem[0x0000] = 0x11;
        apple2.Mem[0xBFFF] = 0x22;

        Assert.Equal((byte)0x11, apple2.Mem[0x0000]);
        Assert.Equal((byte)0x22, apple2.Mem[0xBFFF]);
    }

    [Fact]
    public void Display_Soft_Switches_Track_Text_Graphics_Mixed_Page_And_Resolution()
    {
        var apple2 = BuildApple2();
        var switches = apple2.SoftSwitches;

        Assert.True(switches.TextMode);
        Assert.False(switches.MixedMode);
        Assert.False(switches.Page2);
        Assert.False(switches.HiRes);

        _ = apple2.Mem[Apple2SoftSwitches.GraphicsModeAddress];
        Assert.False(switches.TextMode);
        _ = apple2.Mem[Apple2SoftSwitches.TextModeAddress];
        Assert.True(switches.TextMode);

        _ = apple2.Mem[Apple2SoftSwitches.MixedModeOnAddress];
        Assert.True(switches.MixedMode);
        _ = apple2.Mem[Apple2SoftSwitches.MixedModeOffAddress];
        Assert.False(switches.MixedMode);

        _ = apple2.Mem[Apple2SoftSwitches.HiResModeAddress];
        Assert.True(switches.HiRes);
        _ = apple2.Mem[Apple2SoftSwitches.LoResModeAddress];
        Assert.False(switches.HiRes);
    }

    [Fact]
    public void Page_Soft_Switches_Select_The_Rendered_Text_Page()
    {
        var apple2 = BuildApple2();

        Assert.Equal(Apple2TextScreen.TextPage1BaseAddress, apple2.SoftSwitches.ActiveTextPageBaseAddress);

        _ = apple2.Mem[Apple2SoftSwitches.TextPage2Address];
        Assert.True(apple2.SoftSwitches.Page2);
        Assert.Equal(Apple2TextScreen.TextPage2BaseAddress, apple2.SoftSwitches.ActiveTextPageBaseAddress);

        _ = apple2.Mem[Apple2SoftSwitches.TextPage1Address];
        Assert.False(apple2.SoftSwitches.Page2);
        Assert.Equal(Apple2TextScreen.TextPage1BaseAddress, apple2.SoftSwitches.ActiveTextPageBaseAddress);
    }

    [Fact]
    public void Speaker_Toggle_Is_Counted_But_Produces_No_Audio()
    {
        var apple2 = BuildApple2();

        _ = apple2.Mem[Apple2SoftSwitches.SpeakerToggleAddress];
        apple2.Mem[Apple2SoftSwitches.SpeakerToggleAddress] = 0x00;

        Assert.Equal(2UL, apple2.SoftSwitches.SpeakerToggleCount);
    }

    [Theory]
    [InlineData(0xC061)]  // button 0
    [InlineData(0xC064)]  // paddle 0 timer
    public void Buttons_And_Paddles_Read_As_Idle(ushort address)
    {
        var apple2 = BuildApple2();

        Assert.Equal((byte)0x00, apple2.Mem[address]);
    }

    [Fact]
    public void Reset_Restores_The_Default_Display_Switch_State()
    {
        var apple2 = BuildApple2();
        _ = apple2.Mem[Apple2SoftSwitches.GraphicsModeAddress];
        _ = apple2.Mem[Apple2SoftSwitches.TextPage2Address];
        apple2.Keyboard.KeyPressed((byte)'A');

        apple2.Reset(cpuStartPos: 0x0000);

        Assert.True(apple2.SoftSwitches.TextMode);
        Assert.False(apple2.SoftSwitches.Page2);
        Assert.Equal((byte)0x00, apple2.Keyboard.Latch);
    }
}
