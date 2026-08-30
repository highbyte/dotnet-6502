using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public class OricInputInjectorTests
{
    [Fact]
    public void The_System_Exposes_The_Injector()
    {
        var oric = Build();

        Assert.Same(oric.InputInjector, ((ISystem)oric).InputInjector);
    }

    [Fact]
    public void Available_Keys_Describe_The_Atmos_Keyboard()
    {
        var keys = Build().InputInjector.GetAvailableKeys();

        foreach (var expected in new[] { "a", "0", "space", "return", "shift", "ctrl", "funct", "left" })
            Assert.Contains(expected, keys);
        Assert.DoesNotContain("f12", keys);
    }

    [Fact]
    public void A_Held_Key_Is_Merged_Into_The_Keyboard_Matrix()
    {
        var (oric, handler) = BuildWithHandler();

        oric.InputInjector.HoldKey("h");
        handler.BeforeFrame();

        Assert.True(oric.Keyboard.IsKeyPressed(HostKey.KeyH));
        Assert.True(oric.InputInjector.IsKeyDown("h"));

        oric.InputInjector.ReleaseHeldKey("h");
        handler.BeforeFrame();
        Assert.False(oric.Keyboard.IsKeyPressed(HostKey.KeyH));
    }

    [Fact]
    public void A_Frame_Injected_Key_Lasts_One_Frame()
    {
        var (oric, handler) = BuildWithHandler();

        oric.InputInjector.KeyPress("return");
        handler.BeforeFrame();
        Assert.True(oric.Keyboard.IsKeyPressed(HostKey.Enter));

        oric.InputInjector.BeginFrame();
        handler.BeforeFrame();
        Assert.False(oric.Keyboard.IsKeyPressed(HostKey.Enter));
    }

    [Fact]
    public void Injected_Joystick_Actions_Are_Merged_Per_Port()
    {
        var (oric, handler) = BuildWithHandler(OricJoystickInterface.PASE);

        oric.InputInjector.HoldJoystickAction(2, "left");
        oric.InputInjector.SetJoystickAction(2, "fire", pressed: true);
        handler.BeforeFrame();

        Assert.Contains(JoystickAction.Left, oric.Joystick.CurrentJoystickActions[2]);
        Assert.Contains(JoystickAction.Fire, oric.Joystick.CurrentJoystickActions[2]);
        Assert.True(oric.InputInjector.IsJoystickActionDown(2, "left"));

        oric.InputInjector.ReleaseAllHeldJoystickActions(2);
        oric.InputInjector.BeginFrame();
        handler.BeforeFrame();
        Assert.Empty(oric.Joystick.CurrentJoystickActions[2]);
    }

    [Fact]
    public void Keyboard_Joystick_Consumes_Injected_Wasd_And_Space()
    {
        var config = new OricConfig
        {
            JoystickInterface = OricJoystickInterface.IJK,
            KeyboardJoystickEnabled = true,
            KeyboardJoystick = 1,
        };
        var oric = new OricMachine(config, NullLoggerFactory.Instance);
        var handler = new OricInputHandler(oric);
        handler.Init(new TestHostInputState());

        oric.InputInjector.HoldKey("w");
        oric.InputInjector.HoldKey("space");
        oric.InputInjector.HoldKey("h");
        handler.BeforeFrame();

        Assert.Contains(JoystickAction.Up, oric.Joystick.CurrentJoystickActions[1]);
        Assert.Contains(JoystickAction.Fire, oric.Joystick.CurrentJoystickActions[1]);
        Assert.False(oric.Keyboard.IsKeyPressed(HostKey.KeyW));
        Assert.False(oric.Keyboard.IsKeyPressed(HostKey.Space));
        Assert.True(oric.Keyboard.IsKeyPressed(HostKey.KeyH));
    }

    [Fact]
    public void Unknown_Keys_Actions_And_Ports_Are_Ignored()
    {
        var (oric, handler) = BuildWithHandler(OricJoystickInterface.PASE);

        oric.InputInjector.HoldKey("f12");
        oric.InputInjector.HoldJoystickAction(3, "fire");
        oric.InputInjector.HoldJoystickAction(1, "diagonal");
        handler.BeforeFrame();

        Assert.False(oric.InputInjector.HasInjectedKeys);
        Assert.Empty(oric.Joystick.CurrentJoystickActions[1]);
        Assert.Empty(oric.Joystick.CurrentJoystickActions[2]);
    }

    private static OricMachine Build(OricJoystickInterface joystickInterface = OricJoystickInterface.None)
        => new(new OricConfig { JoystickInterface = joystickInterface }, NullLoggerFactory.Instance);

    private static (OricMachine Oric, OricInputHandler Handler) BuildWithHandler(
        OricJoystickInterface joystickInterface = OricJoystickInterface.None)
    {
        var oric = Build(joystickInterface);
        var handler = new OricInputHandler(oric);
        handler.Init(new TestHostInputState());
        return (oric, handler);
    }

    private sealed class TestHostInputState : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; } = new HashSet<HostKey>();
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();
        public bool CapsLockOn => false;
        public void UpdatePerFrame() { }
    }
}
