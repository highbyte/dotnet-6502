using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricJoystickTests
{
    [Theory]
    [InlineData(JoystickAction.Up, 0x2f)]
    [InlineData(JoystickAction.Down, 0x37)]
    [InlineData(JoystickAction.Left, 0x3e)]
    [InlineData(JoystickAction.Right, 0x3d)]
    [InlineData(JoystickAction.Fire, 0x1f)]
    public void PaseAdapterMapsEveryActionToItsPrinterPortLine(JoystickAction action, byte expected)
    {
        var joystick = new OricJoystick(new OricConfig { JoystickInterface = OricJoystickInterface.PASE });
        joystick.SetJoystickActions(1, [action]);

        var value = joystick.ReadPortAInput(0x80, 0xc0, 0x00, 0x00);

        Assert.Equal(expected, value & 0x3f);
    }

    [Theory]
    [InlineData(JoystickAction.Up, 0x0f)]
    [InlineData(JoystickAction.Down, 0x17)]
    [InlineData(JoystickAction.Left, 0x1d)]
    [InlineData(JoystickAction.Right, 0x1e)]
    [InlineData(JoystickAction.Fire, 0x1b)]
    public void IjkAdapterMapsEveryActionToItsPrinterPortLine(JoystickAction action, byte expected)
    {
        var joystick = new OricJoystick(new OricConfig { JoystickInterface = OricJoystickInterface.IJK });
        joystick.SetJoystickActions(1, [action]);

        var value = joystick.ReadPortAInput(0x40, 0xc0, 0x00, 0x10);

        Assert.Equal(expected, value & 0x3f);
    }

    [Fact]
    public void PaseAdapterReturnsActiveLowInputsForBothSockets()
    {
        var oric = BuildOric(OricJoystickInterface.PASE);
        oric.Joystick.SetJoystickActions(1, [JoystickAction.Left, JoystickAction.Down]);
        oric.Joystick.SetJoystickActions(2, [JoystickAction.Right, JoystickAction.Fire]);
        oric.Mem[0x0303] = 0xc0;

        oric.Mem[0x0301] = 0x80;
        var port1 = oric.Mem[0x0301];
        oric.Mem[0x0301] = 0x40;
        var port2 = oric.Mem[0x0301];

        Assert.Equal(0x36, port1 & 0x3f);
        Assert.Equal(0x1d, port2 & 0x3f);
        Assert.NotEqual(0, port1 & 0x20);
        Assert.Equal(0, port2 & 0x20);
    }

    [Fact]
    public void IjkAdapterUsesOppositeSocketSelectorsAndPrinterStrobe()
    {
        var oric = BuildOric(OricJoystickInterface.IJK);
        oric.Joystick.SetJoystickActions(1, [JoystickAction.Up, JoystickAction.Fire]);
        oric.Joystick.SetJoystickActions(2, [JoystickAction.Left, JoystickAction.Down]);
        oric.Mem[0x0302] = 0x10;
        oric.Mem[0x0300] = 0x00;
        oric.Mem[0x0303] = 0xc0;

        oric.Mem[0x0301] = 0x40;
        var port1 = oric.Mem[0x0301];
        oric.Mem[0x0301] = 0x80;
        var port2 = oric.Mem[0x0301];

        Assert.Equal(0x0b, port1 & 0x1f);
        Assert.Equal(0x15, port2 & 0x1f);
        Assert.Equal(0, port1 & 0x20);
        Assert.Equal(0, port2 & 0x20);

        oric.Mem[0x0300] = 0x10;
        oric.Mem[0x0301] = 0x40;
        Assert.Equal(0x3f, oric.Mem[0x0301] & 0x3f);
    }

    [Theory]
    [InlineData(JoystickAction.Up, 0x10)]
    [InlineData(JoystickAction.Down, 0x08)]
    [InlineData(JoystickAction.Left, 0x02)]
    [InlineData(JoystickAction.Right, 0x01)]
    [InlineData(JoystickAction.Fire, 0x04)]
    public void StormlordIjkReadSequenceDetectsEveryAction(JoystickAction action, byte expected)
    {
        var oric = BuildOric(OricJoystickInterface.IJK);
        oric.Joystick.SetJoystickActions(1, [action]);

        // Exact VIA setup and reads used by Stormlord's ReadIJK routine.
        oric.Mem[0x0302] = 0xb7;
        oric.Mem[0x0300] = 0x00;
        oric.Mem[0x0303] = 0xc0;
        oric.Mem[0x0301] = 0x7f;
        var port1 = (byte)((oric.Mem[0x0301] & 0x1f) ^ 0x1f);
        oric.Mem[0x0301] = 0xbf;
        var port2 = (byte)((oric.Mem[0x0301] & 0x1f) ^ 0x1f);

        Assert.Equal(expected, port1);
        Assert.Equal(0, port2);
    }

    [Fact]
    public void NoAdapterLeavesPrinterPortInputsHigh()
    {
        var oric = BuildOric(OricJoystickInterface.None);
        oric.Joystick.SetJoystickActions(1, [JoystickAction.Up, JoystickAction.Fire]);
        oric.Mem[0x0303] = 0xc0;
        oric.Mem[0x0301] = 0x80;

        Assert.Equal(0x3f, oric.Mem[0x0301] & 0x3f);
    }

    [Fact]
    public void InputHandlerRoutesGamepadAndKeyboardJoystickToSeparateSockets()
    {
        var oric = new OricMachine(new OricConfig
        {
            JoystickInterface = OricJoystickInterface.PASE,
            KeyboardJoystickEnabled = true,
            KeyboardJoystick = 2,
        }, NullLoggerFactory.Instance);
        var inputConfig = new OricInputConfig { CurrentJoystick = 1 };
        var handler = new OricInputHandler(oric, inputConfig);
        handler.Init(new TestHostInputState(
            new HashSet<HostKey> { HostKey.KeyW, HostKey.Space, HostKey.Enter },
            new HashSet<GamepadButton> { GamepadButton.DPadLeft }));

        handler.BeforeFrame();

        Assert.Contains(JoystickAction.Left, oric.Joystick.CurrentJoystickActions[1]);
        Assert.Contains(JoystickAction.Up, oric.Joystick.CurrentJoystickActions[2]);
        Assert.Contains(JoystickAction.Fire, oric.Joystick.CurrentJoystickActions[2]);
        Assert.NotEqual(0, oric.Keyboard.ReadRow(6) & 0x80);
        Assert.NotEqual(0, oric.Keyboard.ReadRow(4) & 0x01);
        Assert.Equal(0, oric.Keyboard.ReadRow(7) & 0x20);
    }

    [Fact]
    public void DisabledKeyboardJoystickLeavesItsKeysInTheAtmosMatrix()
    {
        var oric = BuildOric(OricJoystickInterface.PASE);
        var handler = new OricInputHandler(oric);
        handler.Init(new TestHostInputState(
            new HashSet<HostKey> { HostKey.KeyW, HostKey.Space },
            new HashSet<GamepadButton>()));

        handler.BeforeFrame();

        Assert.Empty(oric.Joystick.CurrentJoystickActions[1]);
        Assert.Empty(oric.Joystick.CurrentJoystickActions[2]);
        Assert.Equal(0, oric.Keyboard.ReadRow(6) & 0x80);
        Assert.Equal(0, oric.Keyboard.ReadRow(4) & 0x01);
    }

    [Fact]
    public void SystemConfigurationClonePreservesJoystickSettings()
    {
        var source = new OricSystemConfig
        {
            JoystickInterface = OricJoystickInterface.IJK,
            KeyboardJoystickEnabled = true,
            KeyboardJoystick = 2,
        };

        var clone = (OricSystemConfig)source.Clone();

        Assert.Equal(OricJoystickInterface.IJK, clone.JoystickInterface);
        Assert.True(clone.KeyboardJoystickEnabled);
        Assert.Equal(2, clone.KeyboardJoystick);
    }

    [Fact]
    public void SystemConfigurationRejectsInvalidJoystickSelections()
    {
        var config = new OricSystemConfig
        {
            JoystickInterface = (OricJoystickInterface)99,
            KeyboardJoystick = 3,
        };

        config.IsValid(out var validationErrors);

        Assert.Contains(validationErrors, error => error.Contains("joystick interface", StringComparison.Ordinal));
        Assert.Contains(validationErrors, error => error.Contains("KeyboardJoystick", StringComparison.Ordinal));
    }

    private static OricMachine BuildOric(OricJoystickInterface joystickInterface)
        => new(new OricConfig { JoystickInterface = joystickInterface }, NullLoggerFactory.Instance);

    private sealed class TestHostInputState(
        IReadOnlySet<HostKey> keysDown,
        IReadOnlySet<GamepadButton> gamepadButtonsDown) : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; } = keysDown;
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = gamepadButtonsDown;
        public bool CapsLockOn => false;
        public void UpdatePerFrame() { }
    }
}
