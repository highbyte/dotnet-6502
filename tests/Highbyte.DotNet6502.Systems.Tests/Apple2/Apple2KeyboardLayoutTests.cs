using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;
using TestHostInputState = Highbyte.DotNet6502.Systems.Tests.Apple2.Apple2InputHandlerTests.TestHostInputState;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// The Apple II keyboard map per host layout, and how the layout is resolved.
///
/// ASCII values here are the bare 7-bit codes the map produces; the latch adds the high bit, which
/// is why <see cref="Apple2InputHandlerTests"/> asserts 0xC1 where this file asserts 'A'.
/// </summary>
public class Apple2KeyboardLayoutTests
{
    // ------------------------------------------------------------------ the maps

    [Theory]
    // Shifted digits.
    [InlineData(HostKey.Digit2, true, '@')]
    [InlineData(HostKey.Digit6, true, '^')]
    [InlineData(HostKey.Digit7, true, '&')]
    [InlineData(HostKey.Digit8, true, '*')]
    // Punctuation.
    [InlineData(HostKey.Minus, false, '-')]
    [InlineData(HostKey.Minus, true, '_')]
    [InlineData(HostKey.Equal, false, '=')]
    [InlineData(HostKey.Equal, true, '+')]
    [InlineData(HostKey.Semicolon, false, ';')]
    [InlineData(HostKey.Semicolon, true, ':')]
    [InlineData(HostKey.Slash, false, '/')]
    [InlineData(HostKey.Slash, true, '?')]
    [InlineData(HostKey.BracketLeft, false, '[')]
    [InlineData(HostKey.Backslash, false, '\\')]
    [InlineData(HostKey.Quote, false, '\'')]
    // The map emits the true code even where the II Plus has no glyph for it: '|' is $7C, and the
    // Autostart ROM's input routine folds $E0-$FF down a bit-5 step on echo, so this one reaches
    // Applesoft as '|' but shows on screen as '\'. Verified against the running machine by reading
    // the keyboard latch at $C000 directly.
    [InlineData(HostKey.Backslash, true, '|')]
    [InlineData(HostKey.BracketLeft, true, '{')]
    public void US_Layout_Produces_The_Expected_Character(HostKey key, bool shift, char expected)
    {
        var keyboard = new Apple2HostKeyboard(HostKeyboardLayout.US);

        Assert.True(keyboard.TryGetAscii(key, shift, control: false, out var ascii));
        Assert.Equal((byte)expected, ascii);
    }

    [Theory]
    // Shifted digits differ from US almost everywhere.
    [InlineData(HostKey.Digit2, true, '"')]
    [InlineData(HostKey.Digit6, true, '&')]
    [InlineData(HostKey.Digit7, true, '/')]
    [InlineData(HostKey.Digit8, true, '(')]
    [InlineData(HostKey.Digit9, true, ')')]
    [InlineData(HostKey.Digit0, true, '=')]
    // Right of 0 is +/? on a Swedish keyboard, not -/_.
    [InlineData(HostKey.Minus, false, '+')]
    [InlineData(HostKey.Minus, true, '?')]
    // -/_ moves to where US has /?.
    [InlineData(HostKey.Slash, false, '-')]
    [InlineData(HostKey.Slash, true, '_')]
    // ;/: move onto the comma and period keys.
    [InlineData(HostKey.Comma, true, ';')]
    [InlineData(HostKey.Period, true, ':')]
    // The '/* key right of Ä.
    [InlineData(HostKey.Backslash, false, '\'')]
    [InlineData(HostKey.Backslash, true, '*')]
    // The <> key left of Z, which US keyboards do not have.
    [InlineData(HostKey.IntlBackslash, false, '<')]
    [InlineData(HostKey.IntlBackslash, true, '>')]
    // ^ is Applesoft's exponent operator; it lives on shifted ¨.
    [InlineData(HostKey.BracketRight, true, '^')]
    // Å/Ä/Ö convenience bindings — no ASCII form of their own.
    [InlineData(HostKey.BracketLeft, false, '[')]
    [InlineData(HostKey.Quote, false, ']')]
    [InlineData(HostKey.Semicolon, false, '\\')]
    public void Swedish_Layout_Produces_The_Expected_Character(HostKey key, bool shift, char expected)
    {
        var keyboard = new Apple2HostKeyboard(HostKeyboardLayout.Swedish);

        Assert.True(keyboard.TryGetAscii(key, shift, control: false, out var ascii));
        Assert.Equal((byte)expected, ascii);
    }

    [Theory]
    [InlineData(HostKey.Digit2, '@')]
    [InlineData(HostKey.Digit4, '$')]
    [InlineData(HostKey.Digit8, '[')]
    [InlineData(HostKey.Digit9, ']')]
    public void Swedish_Alt_Chords_Beat_The_Digit_They_Are_Built_From(HostKey key, char expected)
    {
        var keyboard = new Apple2HostKeyboard(HostKeyboardLayout.Swedish);

        Assert.True(keyboard.TryGetAscii(key, shift: false, control: false, out var ascii, alt: true));
        Assert.Equal((byte)expected, ascii);

        // Without Alt the same key is still the plain digit.
        Assert.True(keyboard.TryGetAscii(key, shift: false, control: false, out var plain));
        Assert.Equal((byte)('0' + (key - HostKey.Digit0)), plain);
    }

    [Fact]
    public void Alt_Chords_Do_Not_Apply_On_The_US_Layout()
    {
        var keyboard = new Apple2HostKeyboard(HostKeyboardLayout.US);

        Assert.True(keyboard.TryGetAscii(HostKey.Digit8, shift: false, control: false, out var ascii, alt: true));
        Assert.Equal((byte)'8', ascii);
    }

    [Fact]
    public void A_Swedish_Dead_Key_Produces_No_Character_Unshifted()
    {
        var keyboard = new Apple2HostKeyboard(HostKeyboardLayout.Swedish);

        // The ¨ key: nothing on its own, ^ when shifted.
        Assert.False(keyboard.TryGetAscii(HostKey.BracketRight, shift: false, control: false, out _));
        Assert.True(keyboard.TryGetAscii(HostKey.BracketRight, shift: true, control: false, out var shifted));
        Assert.Equal((byte)'^', shifted);

        // A key with no character in either state is not latchable at all.
        Assert.False(keyboard.ProducesCharacter(HostKey.Backquote));
    }

    [Theory]
    [InlineData(HostKeyboardLayout.US)]
    [InlineData(HostKeyboardLayout.Swedish)]
    public void Letters_Digits_And_Control_Keys_Are_The_Same_On_Every_Layout(HostKeyboardLayout layout)
    {
        var keyboard = new Apple2HostKeyboard(layout);

        Assert.True(keyboard.TryGetAscii(HostKey.KeyA, shift: true, control: false, out var a));
        Assert.Equal((byte)'A', a);   // uppercase in both states — no lowercase generator

        Assert.True(keyboard.TryGetAscii(HostKey.Digit5, shift: false, control: false, out var five));
        Assert.Equal((byte)'5', five);

        Assert.True(keyboard.TryGetAscii(HostKey.Enter, shift: false, control: false, out var ret));
        Assert.Equal(0x0D, ret);

        Assert.True(keyboard.TryGetAscii(HostKey.KeyC, shift: false, control: true, out var ctrlC));
        Assert.Equal(0x03, ctrlC);
    }

    // ------------------------------------------------------------- layout resolution

    [Fact]
    public void An_Explicit_Config_Setting_Wins_Over_Detection()
    {
        var inputState = new TestHostInputState { NativeKeyboardLayoutId = "com.apple.keylayout.US" };
        var handler = BuildHandler(new Apple2InputConfig { KeyboardLayout = HostKeyboardLayout.Swedish }, inputState);

        Assert.Equal(HostKeyboardLayout.Swedish, handler.HostKeyboard.Layout);
    }

    [Theory]
    [InlineData("com.apple.keylayout.Swedish", HostKeyboardLayout.Swedish)]
    [InlineData("com.apple.keylayout.US", HostKeyboardLayout.US)]
    [InlineData("0000041D", HostKeyboardLayout.Swedish)]
    [InlineData("00000409", HostKeyboardLayout.US)]
    public void An_Unpinned_Layout_Is_Auto_Detected_From_The_Host(string nativeLayoutId, HostKeyboardLayout expected)
    {
        var inputState = new TestHostInputState { NativeKeyboardLayoutId = nativeLayoutId };
        var handler = BuildHandler(new Apple2InputConfig(), inputState);

        Assert.Equal(expected, handler.HostKeyboard.Layout);
    }

    [Fact]
    public void An_Undetectable_Unmapped_Host_Falls_Back_To_US()
    {
        // Neither detectable nor a culture this build maps — the end of the chain.
        var inputState = new TestHostInputState { NativeKeyboardLayoutId = "com.apple.keylayout.Georgian" };
        var handler = BuildHandler(new Apple2InputConfig(), inputState);

        // Culture is whatever the test machine runs, so only assert the fallback is a valid
        // layout and that an unmapped id did not throw or leave the map unbuilt.
        Assert.True(handler.HostKeyboard.Layout is HostKeyboardLayout.US or HostKeyboardLayout.Swedish);
        Assert.NotEmpty(handler.HostKeyboard.HostKeyToAsciiMap);
    }

    // --------------------------------------------------- macOS ISO keyboard correction

    [Fact]
    public void MacOS_Swaps_Backquote_And_IntlBackslash_On_A_Non_US_Layout()
    {
        var (apple2, handler, inputState) = BuildForMac(HostKeyboardLayout.Swedish);

        // On a macOS ISO keyboard the physical <> key arrives as Backquote. After the correction
        // it is looked up as IntlBackslash, which the Swedish map binds to '<'.
        inputState.SetKeysDown(HostKey.Backquote);
        handler.BeforeFrame();

        Assert.Equal((byte)('<' | 0x80), apple2.Keyboard.Latch);
    }

    [Fact]
    public void MacOS_Does_Not_Swap_On_The_US_Layout()
    {
        var (apple2, handler, inputState) = BuildForMac(HostKeyboardLayout.US);

        // A US keyboard is ANSI, so Backquote really is the ` key and must stay as it is.
        inputState.SetKeysDown(HostKey.Backquote);
        handler.BeforeFrame();

        Assert.Equal((byte)('`' | 0x80), apple2.Keyboard.Latch);
    }

    private static Apple2InputHandler BuildHandler(Apple2InputConfig inputConfig, TestHostInputState inputState)
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);
        var handler = new Apple2InputHandler(apple2, NullLoggerFactory.Instance, inputConfig);
        handler.Init(inputState);
        return handler;
    }

    private static (Apple2System Apple2, Apple2InputHandler Handler, TestHostInputState InputState) BuildForMac(
        HostKeyboardLayout layout)
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);
        var inputState = new TestHostInputState { IsRunningOnMacOS = true };
        var inputConfig = new Apple2InputConfig { KeyboardLayout = layout };
        var handler = new Apple2InputHandler(apple2, NullLoggerFactory.Instance, inputConfig);
        handler.Init(inputState);
        return (apple2, handler, inputState);
    }
}
