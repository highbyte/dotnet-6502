using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public class OricKeyboardLayoutTests
{
    [Theory]
    [InlineData(HostKey.Digit2, true, HostKey.Quote, true)]
    [InlineData(HostKey.Digit6, true, HostKey.Digit7, true)]
    [InlineData(HostKey.Digit7, true, HostKey.Slash, false)]
    [InlineData(HostKey.Digit8, true, HostKey.Digit9, true)]
    [InlineData(HostKey.Digit9, true, HostKey.Digit0, true)]
    [InlineData(HostKey.Digit0, true, HostKey.Equal, false)]
    [InlineData(HostKey.Minus, false, HostKey.Equal, true)]
    [InlineData(HostKey.Minus, true, HostKey.Slash, true)]
    [InlineData(HostKey.Slash, false, HostKey.Minus, false)]
    [InlineData(HostKey.Slash, true, HostKey.Minus, true)]
    [InlineData(HostKey.Comma, true, HostKey.Semicolon, false)]
    [InlineData(HostKey.Period, true, HostKey.Semicolon, true)]
    [InlineData(HostKey.Backslash, false, HostKey.Quote, false)]
    [InlineData(HostKey.Backslash, true, HostKey.Digit8, true)]
    [InlineData(HostKey.IntlBackslash, false, HostKey.Comma, true)]
    [InlineData(HostKey.IntlBackslash, true, HostKey.Period, true)]
    [InlineData(HostKey.BracketRight, true, HostKey.Digit6, true)]
    [InlineData(HostKey.Quote, false, HostKey.BracketRight, false)]
    [InlineData(HostKey.Semicolon, false, HostKey.Backslash, false)]
    public void Swedish_Layout_Maps_Host_Symbols_To_Atmos_Matrix_Chords(
        HostKey sourceKey,
        bool sourceShift,
        HostKey targetKey,
        bool targetShift)
    {
        var keyboard = new OricHostKeyboard(HostKeyboardLayout.Swedish);
        var source = new HashSet<HostKey> { sourceKey };
        if (sourceShift)
            source.Add(HostKey.ShiftLeft);
        var translated = new HashSet<HostKey>();

        keyboard.Translate(source, translated);

        Assert.Contains(targetKey, translated);
        Assert.Equal(targetShift, translated.Contains(HostKey.ShiftRight));
        Assert.DoesNotContain(HostKey.ShiftLeft, translated);
    }

    [Theory]
    [InlineData(HostKey.Digit2, HostKey.Digit2, true)]
    [InlineData(HostKey.Digit4, HostKey.Digit4, true)]
    [InlineData(HostKey.Digit8, HostKey.BracketLeft, false)]
    [InlineData(HostKey.Digit9, HostKey.BracketRight, false)]
    public void Swedish_Alt_Chords_Map_Common_MacOS_And_Windows_Symbols(
        HostKey sourceKey,
        HostKey targetKey,
        bool targetShift)
    {
        var keyboard = new OricHostKeyboard(HostKeyboardLayout.Swedish);
        var translated = new HashSet<HostKey>();

        keyboard.Translate(new HashSet<HostKey> { HostKey.AltRight, sourceKey }, translated);

        Assert.Contains(targetKey, translated);
        Assert.Equal(targetShift, translated.Contains(HostKey.ShiftRight));
        Assert.DoesNotContain(HostKey.AltLeft, translated);
        Assert.DoesNotContain(HostKey.AltRight, translated);
    }

    [Fact]
    public void Other_Alt_Combinations_Preserve_Atmos_Funct_And_The_Swedish_Base_Mapping()
    {
        var keyboard = new OricHostKeyboard(HostKeyboardLayout.Swedish);
        var translated = new HashSet<HostKey>();

        keyboard.Translate(
            new HashSet<HostKey> { HostKey.AltLeft, HostKey.Semicolon },
            translated);

        Assert.Contains(HostKey.AltLeft, translated);
        Assert.Contains(HostKey.Backslash, translated);
        Assert.DoesNotContain(HostKey.Semicolon, translated);
    }

    [Theory]
    [InlineData(HostKey.BracketRight)]
    [InlineData(HostKey.Equal)]
    [InlineData(HostKey.Backquote)]
    public void Swedish_Dead_Or_Non_Ascii_Keys_Are_Unbound_Unshifted(HostKey sourceKey)
    {
        var keyboard = new OricHostKeyboard(HostKeyboardLayout.Swedish);
        var translated = new HashSet<HostKey>();

        keyboard.Translate(new HashSet<HostKey> { sourceKey }, translated);

        Assert.Empty(translated);
    }

    [Fact]
    public void US_Layout_Preserves_Physical_Matrix_Keys()
    {
        var keyboard = new OricHostKeyboard(HostKeyboardLayout.US);
        var source = new HashSet<HostKey> { HostKey.ShiftLeft, HostKey.Digit2, HostKey.AltLeft };
        var translated = new HashSet<HostKey>();

        keyboard.Translate(source, translated);

        Assert.Equal(source, translated);
    }

    [Fact]
    public void An_Explicit_Config_Setting_Wins_Over_Detection()
    {
        var state = new TestHostInputState { NativeKeyboardLayoutId = "com.apple.keylayout.US" };
        var handler = BuildHandler(
            new OricInputConfig { KeyboardLayout = HostKeyboardLayout.Swedish },
            state);

        Assert.Equal(HostKeyboardLayout.Swedish, handler.HostKeyboard.Layout);
    }

    [Theory]
    [InlineData("com.apple.keylayout.Swedish", HostKeyboardLayout.Swedish)]
    [InlineData("com.apple.keylayout.US", HostKeyboardLayout.US)]
    [InlineData("0000041D", HostKeyboardLayout.Swedish)]
    [InlineData("00000409", HostKeyboardLayout.US)]
    public void Auto_Layout_Uses_The_Detected_Host_Layout(
        string nativeLayoutId,
        HostKeyboardLayout expected)
    {
        var state = new TestHostInputState { NativeKeyboardLayoutId = nativeLayoutId };
        var handler = BuildHandler(new OricInputConfig(), state);

        Assert.Equal(expected, handler.HostKeyboard.Layout);
    }

    [Fact]
    public void MacOS_ISO_Correction_Maps_The_Physical_Less_Than_Key()
    {
        var state = new TestHostInputState { IsRunningOnMacOS = true };
        var handler = BuildHandler(
            new OricInputConfig { KeyboardLayout = HostKeyboardLayout.Swedish },
            state,
            out var oric);

        // macOS reports the Swedish ISO <> key as Backquote. It is swapped to IntlBackslash,
        // which translates to the Atmos Shift+Comma chord for '<'.
        state.SetKeysDown(HostKey.Backquote);
        handler.BeforeFrame();

        Assert.True(oric.Keyboard.IsKeyPressed(HostKey.Comma));
        Assert.True(oric.Keyboard.IsKeyPressed(HostKey.ShiftRight));
        Assert.False(oric.Keyboard.IsKeyPressed(HostKey.Backquote));
    }

    private static OricInputHandler BuildHandler(
        OricInputConfig inputConfig,
        TestHostInputState state)
        => BuildHandler(inputConfig, state, out _);

    private static OricInputHandler BuildHandler(
        OricInputConfig inputConfig,
        TestHostInputState state,
        out OricMachine oric)
    {
        oric = new OricMachine(new OricConfig(), NullLoggerFactory.Instance);
        var handler = new OricInputHandler(oric, NullLoggerFactory.Instance, inputConfig);
        handler.Init(state);
        return handler;
    }

    private sealed class TestHostInputState : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; private set; } = new HashSet<HostKey>();
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();
        public bool CapsLockOn => false;
        public bool IsRunningOnMacOS { get; set; }
        public string? NativeKeyboardLayoutId { get; set; }

        public string? DetectNativeKeyboardLayoutId() => NativeKeyboardLayoutId;
        public void SetKeysDown(params HostKey[] keys) => KeysDown = new HashSet<HostKey>(keys);
        public void UpdatePerFrame() { }
    }
}
