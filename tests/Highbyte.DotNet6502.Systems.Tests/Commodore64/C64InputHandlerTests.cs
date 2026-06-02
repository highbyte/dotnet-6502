using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class C64InputHandlerTests
{
    [Fact]
    public void BeforeFrame_DoesNotAllocate_ForHeldKeyboardGamepadAndKeyboardJoystickInput()
    {
        var c64 = BuildC64(keyboardJoystickEnabled: true);
        var inputHandler = new C64InputHandler(c64, NullLoggerFactory.Instance, new C64InputConfig());
        var inputState = new TestHostInputState(
            keysDown: new HashSet<HostKey> { HostKey.KeyW, HostKey.Space, HostKey.Enter },
            gamepadButtonsDown: new HashSet<GamepadButton> { GamepadButton.DPadLeft });

        inputHandler.Init(inputState);

        WarmUp(inputHandler);

        long allocatedBytes = MeasureAllocatedBytes(() =>
        {
            for (int i = 0; i < 100; i++)
                inputHandler.BeforeFrame();
        });

        Assert.Equal(0, allocatedBytes);
        Assert.True(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.Return));
        Assert.False(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.W));
        Assert.False(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.Space));
        Assert.Contains(C64JoystickAction.Up, c64.Cia1.Joystick.CurrentJoystickActions[2]);
        Assert.Contains(C64JoystickAction.Fire, c64.Cia1.Joystick.CurrentJoystickActions[2]);
        Assert.Contains(C64JoystickAction.Left, c64.Cia1.Joystick.CurrentJoystickActions[2]);
    }

    [Fact]
    public void BeforeFrame_DoesNotAllocate_ForMacOsIsoKeyboardSwap()
    {
        var c64 = BuildC64();
        var inputConfig = new C64InputConfig
        {
            KeyboardLayout = C64KeyboardLayout.Swedish
        };
        var inputHandler = new C64InputHandler(c64, NullLoggerFactory.Instance, inputConfig);
        var inputState = new TestHostInputState(
            keysDown: new HashSet<HostKey> { HostKey.Backquote },
            gamepadButtonsDown: new HashSet<GamepadButton>(),
            isRunningOnMacOS: true);

        inputHandler.Init(inputState);

        WarmUp(inputHandler);

        long allocatedBytes = MeasureAllocatedBytes(() =>
        {
            for (int i = 0; i < 100; i++)
                inputHandler.BeforeFrame();
        });

        Assert.Equal(0, allocatedBytes);
        Assert.True(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.RShift));
        Assert.True(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.Comma));
    }

    private static C64 BuildC64(bool keyboardJoystickEnabled = false)
    {
        return C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
            KeyboardJoystickEnabled = keyboardJoystickEnabled,
            KeyboardJoystick = 2
        }, NullLoggerFactory.Instance);
    }

    private static void WarmUp(C64InputHandler inputHandler)
    {
        for (int i = 0; i < 10; i++)
            inputHandler.BeforeFrame();
    }

    private static long MeasureAllocatedBytes(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private sealed class TestHostInputState : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; }
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; }
        public bool CapsLockOn => false;
        public bool IsRunningOnMacOS { get; }

        public TestHostInputState(
            IReadOnlySet<HostKey> keysDown,
            IReadOnlySet<GamepadButton> gamepadButtonsDown,
            bool isRunningOnMacOS = false)
        {
            KeysDown = keysDown;
            GamepadButtonsDown = gamepadButtonsDown;
            IsRunningOnMacOS = isRunningOnMacOS;
        }

        public void UpdatePerFrame()
        {
        }
    }
}
