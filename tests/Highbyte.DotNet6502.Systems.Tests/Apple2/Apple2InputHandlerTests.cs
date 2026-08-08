using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2InputHandlerTests
{
    /// <summary>
    /// Builds a handler with the keyboard layout pinned, so these assertions never depend on the
    /// developer's own keyboard layout or OS culture. US unless a test asks for something else —
    /// auto-detection is covered by its own tests below.
    /// </summary>
    private static (Apple2System Apple2, Apple2InputHandler Handler, TestHostInputState InputState) Build(
        HostKeyboardLayout layout = HostKeyboardLayout.US)
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);
        var inputState = new TestHostInputState();
        var inputConfig = new Apple2InputConfig { KeyboardLayout = layout };
        var handler = new Apple2InputHandler(apple2, NullLoggerFactory.Instance, inputConfig);
        handler.Init(inputState);
        return (apple2, handler, inputState);
    }

    [Theory]
    [InlineData(HostKey.KeyA, false, false, 0xC1)]   // 'A'
    [InlineData(HostKey.KeyA, true, false, 0xC1)]    // shift does not produce lowercase
    [InlineData(HostKey.Digit1, false, false, 0xB1)] // '1'
    [InlineData(HostKey.Digit1, true, false, 0xA1)]  // '!'
    [InlineData(HostKey.Space, false, false, 0xA0)]
    [InlineData(HostKey.Enter, false, false, 0x8D)]  // RETURN
    [InlineData(HostKey.Escape, false, false, 0x9B)] // ESC
    [InlineData(HostKey.ArrowLeft, false, false, 0x88)]
    [InlineData(HostKey.ArrowRight, false, false, 0x95)]
    [InlineData(HostKey.KeyC, false, true, 0x83)]    // CTRL-C
    public void Key_Press_Latches_The_Expected_Code_With_Strobe(HostKey key, bool shift, bool control, byte expectedLatch)
    {
        var (apple2, handler, inputState) = Build();

        var keys = new HashSet<HostKey> { key };
        if (shift) keys.Add(HostKey.ShiftLeft);
        if (control) keys.Add(HostKey.ControlLeft);
        inputState.SetKeysDown(keys);

        handler.BeforeFrame();

        Assert.Equal(expectedLatch, apple2.Keyboard.Latch);
        Assert.True(apple2.Keyboard.StrobeSet);
    }

    [Fact]
    public void A_Held_Key_Latches_Only_Once_Before_Auto_Repeat_Starts()
    {
        var (apple2, handler, inputState) = Build();
        inputState.SetKeysDown(HostKey.KeyA);

        handler.BeforeFrame();
        apple2.Keyboard.ClearStrobe();

        for (var frame = 1; frame < Apple2InputHandler.AutoRepeatDelayFrames; frame++)
        {
            handler.BeforeFrame();
            Assert.False(apple2.Keyboard.StrobeSet);
        }
    }

    [Fact]
    public void A_Held_Key_Auto_Repeats_After_The_Delay()
    {
        var (apple2, handler, inputState) = Build();
        inputState.SetKeysDown(HostKey.KeyA);

        var repeats = 0;
        for (var frame = 0; frame < Apple2InputHandler.AutoRepeatDelayFrames + Apple2InputHandler.AutoRepeatIntervalFrames * 3; frame++)
        {
            apple2.Keyboard.ClearStrobe();
            handler.BeforeFrame();
            if (apple2.Keyboard.StrobeSet)
                repeats++;
        }

        // The initial press plus one repeat per interval once the delay has elapsed.
        Assert.Equal(1 + 3, repeats);
    }

    [Fact]
    public void Releasing_And_Pressing_The_Same_Key_Latches_Again()
    {
        var (apple2, handler, inputState) = Build();

        inputState.SetKeysDown(HostKey.KeyA);
        handler.BeforeFrame();

        inputState.SetKeysDown();
        handler.BeforeFrame();
        apple2.Keyboard.ClearStrobe();

        inputState.SetKeysDown(HostKey.KeyA);
        handler.BeforeFrame();

        Assert.True(apple2.Keyboard.StrobeSet);
    }

    [Fact]
    public void Modifier_Keys_Alone_Do_Not_Latch_Anything()
    {
        var (apple2, handler, inputState) = Build();
        inputState.SetKeysDown(HostKey.ShiftLeft, HostKey.ControlLeft);

        handler.BeforeFrame();

        Assert.False(apple2.Keyboard.StrobeSet);
        Assert.Equal((byte)0x00, apple2.Keyboard.Latch);
    }

    [Fact]
    public void Unmapped_Keys_Are_Ignored()
    {
        var (apple2, handler, inputState) = Build();
        inputState.SetKeysDown(HostKey.F5);

        handler.BeforeFrame();

        Assert.False(apple2.Keyboard.StrobeSet);
    }

    [Fact]
    public void Ctrl_F12_Performs_A_Warm_Reset()
    {
        var (apple2, handler, inputState) = Build();

        // Leave a stale key in the latch so the reset's effect is observable.
        inputState.SetKeysDown(HostKey.KeyA);
        handler.BeforeFrame();
        Assert.True(apple2.Keyboard.StrobeSet);

        inputState.SetKeysDown(HostKey.ControlLeft, HostKey.F12);
        handler.BeforeFrame();

        // Only Reset clears the keyboard latch and strobe, so this proves it ran.
        Assert.False(apple2.Keyboard.StrobeSet);
        Assert.Equal((byte)0x00, apple2.Keyboard.Latch);
    }

    [Fact]
    public void Holding_Ctrl_F12_Resets_Only_Once()
    {
        var (apple2, handler, inputState) = Build();
        inputState.SetKeysDown(HostKey.ControlLeft, HostKey.F12);

        handler.BeforeFrame();

        // Poke a key into the latch; further frames with the combo still held must not reset again.
        apple2.Keyboard.KeyPressed(0xC1);
        for (var frame = 0; frame < 5; frame++)
            handler.BeforeFrame();

        Assert.True(apple2.Keyboard.StrobeSet);
        Assert.Equal((byte)0xC1, apple2.Keyboard.Latch);
    }

    [Fact]
    public void F12_Without_Ctrl_Does_Not_Reset_Or_Latch()
    {
        var (apple2, handler, inputState) = Build();

        inputState.SetKeysDown(HostKey.KeyA);
        handler.BeforeFrame();
        Assert.True(apple2.Keyboard.StrobeSet);

        inputState.SetKeysDown(HostKey.F12);
        handler.BeforeFrame();

        // No reset: the previously latched key survives.
        Assert.True(apple2.Keyboard.StrobeSet);
        Assert.Equal((byte)0xC1, apple2.Keyboard.Latch);
    }

    [Fact]
    public void Only_One_Code_Is_Latched_When_Several_Keys_Go_Down_Together()
    {
        var (apple2, handler, inputState) = Build();
        inputState.SetKeysDown(HostKey.KeyB, HostKey.KeyA);

        handler.BeforeFrame();

        // The encoder produces one character; the pick is deterministic (lowest HostKey).
        Assert.Equal((byte)0xC1, apple2.Keyboard.Latch);
    }

    internal sealed class TestHostInputState : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; private set; } = new HashSet<HostKey>();
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();
        public bool CapsLockOn => false;
        public bool IsRunningOnMacOS { get; set; }

        /// <summary>
        /// The layout id auto-detection sees. Null by default so tests never reach the real OS —
        /// <see cref="IHostInputState.DetectNativeKeyboardLayoutId"/>'s default implementation
        /// queries the host, which would make every keyboard assertion depend on the layout the
        /// developer happens to be running.
        /// </summary>
        public string? NativeKeyboardLayoutId { get; set; }

        public string? DetectNativeKeyboardLayoutId() => NativeKeyboardLayoutId;

        public void SetKeysDown(params HostKey[] keys) => KeysDown = new HashSet<HostKey>(keys);
        public void SetKeysDown(IReadOnlySet<HostKey> keys) => KeysDown = keys;

        public void UpdatePerFrame()
        {
        }
    }
}
