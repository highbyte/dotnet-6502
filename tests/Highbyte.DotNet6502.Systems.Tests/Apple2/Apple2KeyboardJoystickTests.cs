using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Driving the game port from the host keyboard.
///
/// The interesting half is what the keyboard stops receiving: the mapped keys must reach the stick
/// and <em>not</em> the ASCII latch, because a key cannot both steer and type. WASD is chosen so
/// the Apple II's own arrow keys stay available, and Left Shift so Right Shift still types.
/// </summary>
public class Apple2KeyboardJoystickTests
{
    private static (Apple2System Apple2, Apple2InputHandler Handler, MutableHostInputState Input)
        Build(bool keyboardJoystickEnabled)
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);
        var config = new Apple2InputConfig { KeyboardJoystickEnabled = keyboardJoystickEnabled };
        var handler = new Apple2InputHandler(apple2, NullLoggerFactory.Instance, config);
        var input = new MutableHostInputState();
        handler.Init(input);
        return (apple2, handler, input);
    }

    private static void Press(MutableHostInputState input, Apple2InputHandler handler, params HostKey[] keys)
    {
        input.SetKeys(keys);
        handler.BeforeFrame();
    }

    [Fact]
    public void Off_By_Default_So_The_Mapped_Keys_Still_Belong_To_The_Machine()
    {
        Assert.False(new Apple2InputConfig().KeyboardJoystickEnabled);
    }

    [Fact]
    public void Disabled_The_Arrow_Keys_Leave_The_Stick_Centred()
    {
        var (apple2, handler, input) = Build(keyboardJoystickEnabled: false);

        Press(input, handler, HostKey.KeyA);

        Assert.Equal(Apple2GamePort.PaddleCentre, apple2.GamePort.GetPaddlePosition(0));
    }

    [Theory]
    [InlineData(HostKey.KeyA, 0, Apple2GamePort.PaddleMin)]
    [InlineData(HostKey.KeyD, 0, Apple2GamePort.PaddleMax)]
    [InlineData(HostKey.KeyW, 1, Apple2GamePort.PaddleMin)]
    [InlineData(HostKey.KeyS, 1, Apple2GamePort.PaddleMax)]
    public void Enabled_Each_Direction_Key_Drives_Its_Axis(HostKey key, int paddle, byte expected)
    {
        var (apple2, handler, input) = Build(keyboardJoystickEnabled: true);

        Press(input, handler, key);

        Assert.Equal(expected, apple2.GamePort.GetPaddlePosition(paddle));
    }

    [Fact]
    public void Enabled_Space_Is_The_Button()
    {
        var (apple2, handler, input) = Build(keyboardJoystickEnabled: true);

        Press(input, handler, HostKey.Space);
        Assert.True(apple2.GamePort.IsButtonPressed(0));

        Press(input, handler);
        Assert.False(apple2.GamePort.IsButtonPressed(0));
    }

    [Fact]
    public void Enabled_Left_Shift_Is_The_Second_Button()
    {
        var (apple2, handler, input) = Build(keyboardJoystickEnabled: true);

        Press(input, handler, HostKey.ShiftLeft);
        Assert.True(apple2.GamePort.IsButtonPressed(1));
        Assert.False(apple2.GamePort.IsButtonPressed(0));
    }

    [Fact]
    public void Enabled_The_Second_Button_Combines_With_A_Direction()
    {
        var (apple2, handler, input) = Build(keyboardJoystickEnabled: true);

        Press(input, handler, HostKey.ShiftLeft, HostKey.KeyA);

        Assert.True(apple2.GamePort.IsButtonPressed(1));
        Assert.Equal(Apple2GamePort.PaddleMin, apple2.GamePort.GetPaddlePosition(0));
    }

    [Fact]
    public void Enabled_A_Steering_Key_Does_Not_Also_Type()
    {
        var (apple2, handler, input) = Build(keyboardJoystickEnabled: true);
        apple2.Keyboard.ReadAndClearStrobe();

        Press(input, handler, HostKey.Space);

        // Space would otherwise latch $A0. It steers instead, so nothing reaches the latch.
        Assert.False(apple2.Keyboard.StrobeSet);
        Assert.Equal(Apple2GamePort.PaddleCentre, apple2.GamePort.GetPaddlePosition(0));
        Assert.True(apple2.GamePort.IsButtonPressed(0));
    }

    [Fact]
    public void Disabled_The_Same_Key_Types_As_Normal()
    {
        var (apple2, handler, input) = Build(keyboardJoystickEnabled: false);
        apple2.Keyboard.ReadAndClearStrobe();

        Press(input, handler, HostKey.Space);

        Assert.True(apple2.Keyboard.StrobeSet);
        Assert.Equal(0xA0, apple2.Keyboard.Latch);
    }

    [Fact]
    public void Enabled_An_Unmapped_Key_Still_Reaches_The_Keyboard()
    {
        var (apple2, handler, input) = Build(keyboardJoystickEnabled: true);
        apple2.Keyboard.ReadAndClearStrobe();

        Press(input, handler, HostKey.KeyB);

        Assert.True(apple2.Keyboard.StrobeSet);
        Assert.Equal(0xC2, apple2.Keyboard.Latch);   // 'B'
    }

    [Fact]
    public void Enabled_Steering_And_Typing_Can_Happen_In_The_Same_Frame()
    {
        var (apple2, handler, input) = Build(keyboardJoystickEnabled: true);
        apple2.Keyboard.ReadAndClearStrobe();

        Press(input, handler, HostKey.KeyA, HostKey.KeyB);

        Assert.Equal(Apple2GamePort.PaddleMin, apple2.GamePort.GetPaddlePosition(0));
        Assert.True(apple2.Keyboard.StrobeSet);
        Assert.Equal(0xC2, apple2.Keyboard.Latch);
    }

    /// <summary>
    /// Left Shift steers, but Right Shift must still shift — otherwise turning the joystick on
    /// would cost the ability to type shifted characters entirely.
    /// </summary>
    [Fact]
    public void Enabled_Right_Shift_Still_Works_As_A_Modifier()
    {
        var (apple2, handler, input) = Build(keyboardJoystickEnabled: true);
        apple2.Keyboard.ReadAndClearStrobe();

        Press(input, handler, HostKey.ShiftRight, HostKey.Digit1);

        Assert.False(apple2.GamePort.IsButtonPressed(1));
        Assert.True(apple2.Keyboard.StrobeSet);
        Assert.Equal(0xA1, apple2.Keyboard.Latch);   // '!' — shifted '1'
    }

    [Fact]
    public void A_Gamepad_Works_Whether_Or_Not_The_Keyboard_Joystick_Is_On()
    {
        foreach (var keyboardJoystick in new[] { false, true })
        {
            var (apple2, handler, input) = Build(keyboardJoystick);
            input.SetGamepadButtons(GamepadButton.DPadRight);
            handler.BeforeFrame();

            Assert.Equal(Apple2GamePort.PaddleMax, apple2.GamePort.GetPaddlePosition(0));
        }
    }

    private sealed class MutableHostInputState : IHostInputState
    {
        private HashSet<HostKey> _keys = new();
        private HashSet<GamepadButton> _buttons = new();

        public IReadOnlySet<HostKey> KeysDown => _keys;
        public IReadOnlySet<GamepadButton> GamepadButtonsDown => _buttons;
        public bool CapsLockOn => false;

        public void SetKeys(params HostKey[] keys) => _keys = new HashSet<HostKey>(keys);
        public void SetGamepadButtons(params GamepadButton[] buttons) => _buttons = new HashSet<GamepadButton>(buttons);

        public void UpdatePerFrame() { }
    }
}
