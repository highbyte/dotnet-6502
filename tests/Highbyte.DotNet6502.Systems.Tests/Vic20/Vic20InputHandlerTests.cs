using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20.Input;
using Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Vic20;

public class Vic20InputHandlerTests
{
    [Fact]
    public void CtrlShiftMapsToCommodorePlusShiftForCharsetToggle()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
        var inputHandler = new Vic20InputHandler(vic20, NullLoggerFactory.Instance);

        inputHandler.Init(new TestHostInputState(new HashSet<HostKey> { HostKey.ControlLeft, HostKey.ShiftLeft }));
        inputHandler.BeforeFrame();

        Assert.True(vic20.Via1.Keyboard.IsKeyPressed(Vic20Key.CBM));
        Assert.True(vic20.Via1.Keyboard.IsKeyPressed(Vic20Key.LShift));
    }

    [Fact]
    public void TabMapsToVic20CtrlKey()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
        var inputHandler = new Vic20InputHandler(vic20, NullLoggerFactory.Instance);

        inputHandler.Init(new TestHostInputState(new HashSet<HostKey> { HostKey.Tab }));
        inputHandler.BeforeFrame();

        Assert.True(vic20.Via1.Keyboard.IsKeyPressed(Vic20Key.Ctrl));
    }

    [Fact]
    public void KeyboardMatrixPlacesCommodoreAndLeftShiftWhereKernalExpectsCharsetToggle()
    {
        var keyboard = new Vic20Keyboard(NullLoggerFactory.Instance);
        keyboard.SetKeysPressed(new List<Vic20Key> { Vic20Key.CBM, Vic20Key.LShift }, capsLockOn: false);

        keyboard.SetSelectedColumns(0b1101_0111);

        Assert.Equal(0b1111_1100, keyboard.GetPressedRowsForSelectedColumns());
    }

    [Fact]
    public void KeyboardMatrixPlacesCtrlOnItsOwnTopRowColumn()
    {
        var keyboard = new Vic20Keyboard(NullLoggerFactory.Instance);
        keyboard.SetKeysPressed(new List<Vic20Key> { Vic20Key.Ctrl }, capsLockOn: false);

        keyboard.SetSelectedColumns(0b1111_1011);

        Assert.Equal(0b1111_1110, keyboard.GetPressedRowsForSelectedColumns());
    }

    private sealed class TestHostInputState : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; }
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();
        public bool CapsLockOn => false;

        public TestHostInputState(IReadOnlySet<HostKey> keysDown)
        {
            KeysDown = keysDown;
        }

        public void UpdatePerFrame()
        {
        }
    }
}
