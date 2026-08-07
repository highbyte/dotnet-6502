using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// The game port on its own, driven by a settable cycle clock so the 558 timing can be inspected
/// without running a CPU. The end-to-end contract — that the real ROM's PREAD counts back the
/// position that was set — is <see cref="Apple2RealRomPaddleReadTests"/>.
/// </summary>
public class Apple2GamePortTests
{
    private ulong _cycles;
    private Apple2GamePort BuildPort() => new(() => _cycles);

    private static IReadOnlySet<JoystickAction> Actions(params JoystickAction[] actions)
        => new HashSet<JoystickAction>(actions);

    [Fact]
    public void An_Untriggered_Port_Reads_Every_Paddle_Timer_As_Idle()
    {
        var port = BuildPort();

        // At power-on the cycle counter is 0 too, so "never triggered" must not look like
        // "triggered at cycle 0" — otherwise an untouched port reads as though it were running.
        for (var paddle = 0; paddle < Apple2GamePort.PaddleCount; paddle++)
            Assert.False(port.IsPaddleTimerRunning(paddle));
        Assert.Equal(0x00, port.ReadGamePort(Apple2GamePort.Paddle0Address));
    }

    [Fact]
    public void A_Triggered_Paddle_Holds_Bit_7_For_Its_Position_And_Then_Drops()
    {
        var port = BuildPort();
        port.SetPaddlePosition(0, 100);
        port.Trigger();

        var hold = (ulong)(100 * Apple2GamePort.PreadLoopCycles);

        _cycles = hold - 1;
        Assert.True(port.IsPaddleTimerRunning(0));
        Assert.Equal(0x80, port.ReadGamePort(Apple2GamePort.Paddle0Address));

        _cycles = hold;
        Assert.False(port.IsPaddleTimerRunning(0));
        Assert.Equal(0x00, port.ReadGamePort(Apple2GamePort.Paddle0Address));
    }

    [Fact]
    public void Position_Zero_Expires_Immediately()
    {
        var port = BuildPort();
        port.SetPaddlePosition(0, 0);
        port.Trigger();

        // PREAD counts zero iterations and reports 0, which is the bottom of the range.
        Assert.False(port.IsPaddleTimerRunning(0));
    }

    [Fact]
    public void Retriggering_Restarts_The_Timer()
    {
        var port = BuildPort();
        port.SetPaddlePosition(0, 10);
        port.Trigger();

        _cycles = 10 * Apple2GamePort.PreadLoopCycles;
        Assert.False(port.IsPaddleTimerRunning(0));

        port.Trigger();
        Assert.True(port.IsPaddleTimerRunning(0));
    }

    [Fact]
    public void One_Trigger_Starts_Every_Paddle()
    {
        var port = BuildPort();
        port.SetPaddlePosition(0, 50);
        port.SetPaddlePosition(1, 200);
        port.Trigger();

        _cycles = 100 * Apple2GamePort.PreadLoopCycles;   // past paddle 0, still inside paddle 1
        Assert.False(port.IsPaddleTimerRunning(0));
        Assert.True(port.IsPaddleTimerRunning(1));
    }

    [Theory]
    [InlineData(0xC061, 0)]
    [InlineData(0xC062, 1)]
    [InlineData(0xC063, 2)]
    public void Buttons_Report_In_Bit_7(ushort address, int button)
    {
        var port = BuildPort();

        Assert.Equal(0x00, port.ReadGamePort(address));
        port.SetButton(button, true);
        Assert.Equal(0x80, port.ReadGamePort(address));
    }

    [Fact]
    public void The_Cassette_Input_Address_Is_Not_A_Button()
    {
        var port = BuildPort();
        port.SetButton(0, true);

        // $C060 is cassette in; button 0 is $C061. An off-by-one here would make every game
        // think fire was held.
        Assert.Equal(0x00, port.ReadGamePort(0xC060));
    }

    [Fact]
    public void A_Held_Direction_Drives_Its_Axis_To_The_End_Of_Travel()
    {
        var port = BuildPort();

        port.ApplyJoystickActions(Actions(JoystickAction.Left));
        Assert.Equal(Apple2GamePort.PaddleMin, port.GetPaddlePosition(0));

        port.ApplyJoystickActions(Actions(JoystickAction.Right));
        Assert.Equal(Apple2GamePort.PaddleMax, port.GetPaddlePosition(0));

        port.ApplyJoystickActions(Actions(JoystickAction.Up));
        Assert.Equal(Apple2GamePort.PaddleMin, port.GetPaddlePosition(1));

        port.ApplyJoystickActions(Actions(JoystickAction.Down));
        Assert.Equal(Apple2GamePort.PaddleMax, port.GetPaddlePosition(1));
    }

    [Fact]
    public void Releasing_Returns_The_Stick_To_Centre()
    {
        var port = BuildPort();

        port.ApplyJoystickActions(Actions(JoystickAction.Left, JoystickAction.Up));
        port.ApplyJoystickActions(Actions());

        Assert.Equal(Apple2GamePort.PaddleCentre, port.GetPaddlePosition(0));
        Assert.Equal(Apple2GamePort.PaddleCentre, port.GetPaddlePosition(1));
    }

    [Fact]
    public void Opposite_Directions_Cancel_To_Centre()
    {
        var port = BuildPort();

        port.ApplyJoystickActions(Actions(JoystickAction.Left, JoystickAction.Right));

        Assert.Equal(Apple2GamePort.PaddleCentre, port.GetPaddlePosition(0));
    }

    [Fact]
    public void Diagonals_Move_Both_Axes()
    {
        var port = BuildPort();

        port.ApplyJoystickActions(Actions(JoystickAction.Right, JoystickAction.Up));

        Assert.Equal(Apple2GamePort.PaddleMax, port.GetPaddlePosition(0));
        Assert.Equal(Apple2GamePort.PaddleMin, port.GetPaddlePosition(1));
    }

    [Fact]
    public void Fire_Is_Button_0()
    {
        var port = BuildPort();

        port.ApplyJoystickActions(Actions(JoystickAction.Fire));
        Assert.True(port.IsButtonPressed(0));
        Assert.False(port.IsButtonPressed(1));

        port.ApplyJoystickActions(Actions());
        Assert.False(port.IsButtonPressed(0));
    }

    [Fact]
    public void Fire2_Is_Button_1()
    {
        var port = BuildPort();

        // Choplifter turns the helicopter with button 1 while firing with button 0, so the two
        // must be independent — mapping Fire2 onto button 0 would look like it worked until a
        // game read $C062.
        port.ApplyJoystickActions(Actions(JoystickAction.Fire2));
        Assert.False(port.IsButtonPressed(0));
        Assert.True(port.IsButtonPressed(1));
        Assert.Equal(0x80, port.ReadGamePort(0xC062));

        port.ApplyJoystickActions(Actions(JoystickAction.Fire, JoystickAction.Fire2));
        Assert.True(port.IsButtonPressed(0));
        Assert.True(port.IsButtonPressed(1));

        port.ApplyJoystickActions(Actions());
        Assert.False(port.IsButtonPressed(0));
        Assert.False(port.IsButtonPressed(1));
    }

    [Fact]
    public void Fire_And_A_Direction_Can_Be_Held_Together()
    {
        var port = BuildPort();

        // Turning in Choplifter is fire-plus-direction, so the two must not be exclusive.
        port.ApplyJoystickActions(Actions(JoystickAction.Fire2, JoystickAction.Left));

        Assert.True(port.IsButtonPressed(1));
        Assert.Equal(Apple2GamePort.PaddleMin, port.GetPaddlePosition(0));
    }

    /// <summary>
    /// The counters are a diagnostic for "does this program use the stick at all?", which cannot
    /// be answered by watching the screen — a game that polls the button but never strobes $C070
    /// looks identical to one that does both.
    /// </summary>
    [Fact]
    public void Reads_And_Triggers_Are_Counted_For_Diagnostics()
    {
        var port = BuildPort();
        Assert.Equal(0UL, port.PaddleTriggerCount);
        Assert.Equal(0UL, port.ButtonReadCount);

        port.Trigger();
        port.Trigger();
        Assert.Equal(2UL, port.PaddleTriggerCount);

        port.ReadGamePort(Apple2GamePort.Button0Address);
        port.ReadGamePort(0xC062);
        Assert.Equal(2UL, port.ButtonReadCount);

        // Per button, because "which button does this game poll?" is the useful question.
        Assert.Equal(1UL, port.ButtonReadCounts[0]);
        Assert.Equal(1UL, port.ButtonReadCounts[1]);
        Assert.Equal(0UL, port.ButtonReadCounts[2]);

        // Reading a paddle is not a button read, and does not re-trigger the one-shot.
        port.ReadGamePort(Apple2GamePort.Paddle0Address);
        Assert.Equal(2UL, port.ButtonReadCount);
        Assert.Equal(2UL, port.PaddleTriggerCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Apple2GamePort.PaddleCount)]
    public void An_Out_Of_Range_Paddle_Is_Rejected(int paddle)
    {
        var port = BuildPort();
        Assert.Throws<ArgumentOutOfRangeException>(() => port.SetPaddlePosition(paddle, 0));
    }
}
