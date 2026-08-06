using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2InputInjectorTests
{
    private static (Apple2System Apple2, Apple2InputHandler Handler) Build()
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);
        var handler = new Apple2InputHandler(apple2, NullLoggerFactory.Instance);
        handler.Init(new TestHostInputState());
        return (apple2, handler);
    }

    [Fact]
    public void The_System_Exposes_The_Injector()
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);

        Assert.NotNull(apple2.InputInjector);
        Assert.Same(apple2.InputInjector, ((ISystem)apple2).InputInjector);
    }

    [Fact]
    public void Available_Keys_Include_Letters_Digits_And_Named_Keys()
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);
        var keys = apple2.InputInjector.GetAvailableKeys();

        Assert.Contains("a", keys);
        Assert.Contains("0", keys);
        Assert.Contains("space", keys);
        Assert.Contains("return", keys);
        Assert.Contains("shift", keys);
        Assert.Contains("ctrl", keys);
    }

    [Fact]
    public void No_Joystick_Actions_Are_Exposed()
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);

        Assert.Empty(apple2.InputInjector.GetAvailableJoystickActions());
        Assert.Equal(0, apple2.InputInjector.JoystickPortCount);
        Assert.False(apple2.InputInjector.IsJoystickActionDown(1, "fire"));
    }

    [Fact]
    public void An_Injected_Key_Press_Latches_Its_Ascii_Code()
    {
        var (apple2, handler) = Build();

        apple2.InputInjector.KeyPress("h");
        handler.BeforeFrame();

        Assert.Equal(0xC8, apple2.Keyboard.Latch);   // 'H' | strobe
        Assert.True(apple2.Keyboard.StrobeSet);
    }

    [Fact]
    public void An_Injected_Shift_Modifier_Combines_With_Other_Keys()
    {
        var (apple2, handler) = Build();

        apple2.InputInjector.KeyPress("shift");
        apple2.InputInjector.KeyPress("1");
        handler.BeforeFrame();

        Assert.Equal(0xA1, apple2.Keyboard.Latch);   // '!' | strobe
    }

    [Fact]
    public void A_Frame_Injected_Key_Lasts_One_Frame_Only()
    {
        var (apple2, handler) = Build();

        apple2.InputInjector.KeyPress("h");
        handler.BeforeFrame();
        apple2.Keyboard.ClearStrobe();

        // Next frame: the injection is gone, so no new latch is raised.
        apple2.InputInjector.BeginFrame();
        handler.BeforeFrame();
        Assert.False(apple2.Keyboard.StrobeSet);
    }

    [Fact]
    public void A_Held_Key_Survives_BeginFrame_And_Auto_Repeats()
    {
        var (apple2, handler) = Build();

        apple2.InputInjector.HoldKey("h");

        var latchCount = 0;
        var frames = Apple2InputHandler.AutoRepeatDelayFrames + Apple2InputHandler.AutoRepeatIntervalFrames + 1;
        for (var frame = 0; frame < frames; frame++)
        {
            apple2.InputInjector.BeginFrame();
            handler.BeforeFrame();
            if (apple2.Keyboard.StrobeSet)
            {
                latchCount++;
                apple2.Keyboard.ClearStrobe();
            }
        }

        // The initial edge plus at least one auto-repeat.
        Assert.True(latchCount >= 2, $"Expected at least 2 latches, got {latchCount}.");

        apple2.InputInjector.ReleaseHeldKey("h");
        apple2.InputInjector.BeginFrame();
        handler.BeforeFrame();
        Assert.False(apple2.Keyboard.StrobeSet);
    }

    [Fact]
    public void IsKeyDown_Reflects_Injected_State()
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);
        var injector = apple2.InputInjector;

        Assert.False(injector.IsKeyDown("h"));
        injector.HoldKey("h");
        Assert.True(injector.IsKeyDown("h"));
        injector.Clear();
        Assert.False(injector.IsKeyDown("h"));
    }

    [Fact]
    public void Unknown_Key_Names_Are_Ignored()
    {
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);
        var injector = apple2.InputInjector;

        injector.KeyPress("nosuchkey");
        injector.HoldKey("nosuchkey");

        Assert.False(injector.HasInjectedKeys);
        Assert.False(injector.IsKeyDown("nosuchkey"));
    }

    private sealed class TestHostInputState : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; } = new HashSet<HostKey>();
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();
        public bool CapsLockOn => false;
        public void UpdatePerFrame() { }
    }
}
