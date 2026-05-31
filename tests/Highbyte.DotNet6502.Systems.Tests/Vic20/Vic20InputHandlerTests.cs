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
        var vic20 = BuildVic20();
        var inputHandler = new Vic20InputHandler(vic20, NullLoggerFactory.Instance);

        inputHandler.Init(new TestHostInputState(new HashSet<HostKey> { HostKey.ControlLeft, HostKey.ShiftLeft }));
        inputHandler.BeforeFrame();

        Assert.True(vic20.Via1.Keyboard.IsKeyPressed(Vic20Key.CBM));
        Assert.True(vic20.Via1.Keyboard.IsKeyPressed(Vic20Key.LShift));
    }

    [Fact]
    public void TabMapsToVic20CtrlKey()
    {
        var vic20 = BuildVic20();
        var inputHandler = new Vic20InputHandler(vic20, NullLoggerFactory.Instance);

        inputHandler.Init(new TestHostInputState(new HashSet<HostKey> { HostKey.Tab }));
        inputHandler.BeforeFrame();

        Assert.True(vic20.Via1.Keyboard.IsKeyPressed(Vic20Key.Ctrl));
    }

    [Fact]
    public void KeyboardMatrixPlacesCommodoreAndLeftShiftWhereKernalExpectsCharsetToggle()
    {
        var keyboard = BuildVic20().Via1.Keyboard;
        keyboard.SetKeysPressed(new List<Vic20Key> { Vic20Key.CBM, Vic20Key.LShift }, restorePressed: false, capsLockOn: false);

        keyboard.SetSelectedColumns(0b1101_0111);

        Assert.Equal(0b1111_1100, keyboard.GetPressedRowsForSelectedColumns());
    }

    [Fact]
    public void KeyboardMatrixPlacesCtrlOnItsOwnTopRowColumn()
    {
        var keyboard = BuildVic20().Via1.Keyboard;
        keyboard.SetKeysPressed(new List<Vic20Key> { Vic20Key.Ctrl }, restorePressed: false, capsLockOn: false);

        keyboard.SetSelectedColumns(0b1111_1011);

        Assert.Equal(0b1111_1110, keyboard.GetPressedRowsForSelectedColumns());
    }

    [Fact]
    public void RunStopPlusRestore_Maps_To_RunStop_And_Raises_Nmi()
    {
        var vic20 = BuildVic20();
        var inputHandler = new Vic20InputHandler(vic20, NullLoggerFactory.Instance);

        inputHandler.Init(new TestHostInputState(new HashSet<HostKey> { HostKey.Escape, HostKey.PageUp }));
        inputHandler.BeforeFrame();

        Assert.True(vic20.Via1.Keyboard.IsKeyPressed(Vic20Key.RunStop));
        Assert.True(vic20.CPU.CPUInterrupts.NMILineEnabled);
        Assert.True(vic20.CPU.CPUInterrupts.NMIPending);
    }

    [Fact]
    public void Restore_Does_Not_Map_To_CursorUp_Matrix_Key()
    {
        var vic20 = BuildVic20();
        var inputHandler = new Vic20InputHandler(vic20, NullLoggerFactory.Instance);

        inputHandler.Init(new TestHostInputState(new HashSet<HostKey> { HostKey.PageUp }));
        inputHandler.BeforeFrame();

        Assert.False(vic20.Via1.Keyboard.IsKeyPressed(Vic20Key.CrsrDown));
        Assert.False(vic20.Via1.Keyboard.IsKeyPressed(Vic20Key.LShift));
        Assert.True(vic20.CPU.CPUInterrupts.NMILineEnabled);
    }

    private static Highbyte.DotNet6502.Systems.Vic20.Vic20 BuildVic20()
    {
        return new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
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
