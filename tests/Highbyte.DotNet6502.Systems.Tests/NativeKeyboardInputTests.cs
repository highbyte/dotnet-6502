using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Vic20Machine = Highbyte.DotNet6502.Systems.Vic20.Vic20;
using Highbyte.DotNet6502.Systems.Vic20.Input;
using Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2Machine = Highbyte.DotNet6502.Systems.Apple2.Apple2;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests;

public class NativeKeyboardInputTests
{
    [Fact]
    public void RepeatedCharactersHaveThreePressedFramesAndTwoReleasedFrames()
    {
        var input = new RecordingInput();
        var queue = new TimedKeyboardInput();
        Assert.True(queue.TryEnqueue(input, "aa"));
        var frames = new List<string>();
        for (var frame = 0; frame < 11; frame++)
        {
            input.Pressed = "";
            queue.BeforeFrame(input);
            frames.Add(input.Pressed);
        }
        Assert.Equal(new[] { "a", "a", "a", "", "", "a", "a", "a", "", "", "" }, frames);
    }

    [Fact]
    public void LineEndingsBecomeSingleReturnPressesAndUnsupportedChunksAreRejected()
    {
        var input = new RecordingInput();
        var queue = new TimedKeyboardInput(1, 1);
        Assert.False(queue.TryEnqueue(input, "a😀"));
        Assert.True(queue.TryEnqueue(input, "x\r\ny\n"));
        var output = new List<char>();
        for (var frame = 0; frame < 10; frame++)
        {
            input.Pressed = "";
            queue.BeforeFrame(input);
            output.AddRange(input.Pressed);
        }
        Assert.Equal("x\ry\r", new string(output.ToArray()));
    }

    [Fact]
    public void QueueIsBoundedAndClearOrMachineChangeCancelsPendingInput()
    {
        var input = new RecordingInput();
        var queue = new TimedKeyboardInput();
        Assert.False(queue.TryEnqueue(input, new string('a', 257)));
        Assert.True(queue.TryEnqueue(input, "ab"));
        queue.BeforeFrame(input);
        input.Pressed = "";
        queue.Clear();
        queue.BeforeFrame(input);
        Assert.Empty(input.Pressed);
        Assert.True(queue.TryEnqueue(input, "c"));
        queue.BeforeFrame(new RecordingInput());
        queue.BeforeFrame(input);
        Assert.Empty(input.Pressed);
    }

    [Fact]
    public void C64TypingUpdatesTheMatrixAndLeavesPersistentRemoteKeysAlone()
    {
        var c64 = C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);
        var handler = new C64InputHandler(c64, NullLoggerFactory.Instance, new C64InputConfig());
        handler.Init(new EmptyHostInput());
        c64.InputInjector!.HoldKey("b");
        var queue = new TimedKeyboardInput();
        Assert.True(queue.TryEnqueue(c64.InputInjector, "\"a\b\r"));
        for (var frame = 0; frame < 16; frame++)
        {
            c64.InputInjector.BeginFrame();
            queue.BeforeFrame(c64.InputInjector);
            handler.BeforeFrame();
            Assert.True(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.B));
            if (frame == 0)
            {
                Assert.True(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.LShift));
                Assert.True(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.Two));
            }
            if (frame == 3)
                Assert.False(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.Two));
            if (frame == 5)
                Assert.True(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.A));
            if (frame == 10)
                Assert.True(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.Delete));
            if (frame == 15)
                Assert.True(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.Return));
        }
        queue.Clear();
        c64.InputInjector.BeginFrame();
        handler.BeforeFrame();
        Assert.False(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.Return));
        Assert.True(c64.InputInjector.IsKeyDown("b"));
    }

    [Fact]
    public void Vic20TypingReachesTheViaMatrixAndReleasesItsModifierChord()
    {
        var machine = new Vic20Machine();
        var handler = new Vic20InputHandler(machine, NullLoggerFactory.Instance);
        handler.Init(new EmptyHostInput());
        var input = machine.InputInjector;
        Assert.Same(input, ((ISystem)machine).InputInjector);
        input.PressCharacter('"');
        handler.BeforeFrame();
        Assert.True(machine.Via1.Keyboard.IsKeyPressed(Vic20Key.Two));
        Assert.True(machine.Via1.Keyboard.IsKeyPressed(Vic20Key.LShift));
        input.BeginFrame();
        handler.BeforeFrame();
        Assert.False(machine.Via1.Keyboard.IsKeyPressed(Vic20Key.Two));
        Assert.False(machine.Via1.Keyboard.IsKeyPressed(Vic20Key.LShift));
        input.HoldKey("a");
        input.BeginFrame();
        handler.BeforeFrame();
        Assert.True(machine.Via1.Keyboard.IsKeyPressed(Vic20Key.A));
        input.ReleaseHeldKey("a");
        handler.BeforeFrame();
        Assert.False(machine.Via1.Keyboard.IsKeyPressed(Vic20Key.A));
    }

    [Theory]
    [InlineData('\t')]
    [InlineData('`')]
    [InlineData('~')]
    public void OricRejectsCharactersWithoutAMatrixKey(char character)
    {
        var machine = new OricMachine();
        Assert.False(machine.InputInjector.CanPressCharacter(character));
        machine.InputInjector.PressCharacter(character);
        Assert.Empty(machine.InputInjector.TextKeys);
    }

    [Theory]
    [InlineData(HostKeyboardLayout.US)]
    [InlineData(HostKeyboardLayout.Swedish)]
    public void OricTextUsesMachineMatrixPositionsIndependentOfTheHostLayout(HostKeyboardLayout layout)
    {
        var machine = new OricMachine();
        var handler = new OricInputHandler(machine, new OricInputConfig { KeyboardLayout = layout });
        handler.Init(new EmptyHostInput());
        machine.InputInjector.PressCharacter('"');
        handler.BeforeFrame();
        Assert.True(machine.Keyboard.IsKeyPressed(HostKey.Quote));
        Assert.True(machine.Keyboard.IsKeyPressed(HostKey.ShiftLeft));
        machine.InputInjector.BeginFrame();
        handler.BeforeFrame();
        Assert.False(machine.Keyboard.IsKeyPressed(HostKey.Quote));
    }

    [Theory]
    [InlineData(HostKeyboardLayout.US)]
    [InlineData(HostKeyboardLayout.Swedish)]
    public void Apple2TextProducesOneLatchWritePerPressIncludingRepeatedSymbols(HostKeyboardLayout layout)
    {
        var machine = new Apple2Machine(new Apple2Config(), NullLoggerFactory.Instance);
        var handler = new Apple2InputHandler(machine, NullLoggerFactory.Instance, new Apple2InputConfig { KeyboardLayout = layout });
        handler.Init(new EmptyHostInput());
        var queue = new TimedKeyboardInput();
        Assert.True(queue.TryEnqueue(machine.InputInjector, "\"\""));
        var latchFrames = new List<int>();
        for (var frame = 0; frame < 10; frame++)
        {
            machine.InputInjector.BeginFrame();
            queue.BeforeFrame(machine.InputInjector);
            handler.BeforeFrame();
            if (machine.Keyboard.StrobeSet)
            {
                Assert.Equal((byte)('"' | 0x80), machine.Keyboard.Latch);
                latchFrames.Add(frame);
                machine.Keyboard.ClearStrobe();
            }
        }
        Assert.Equal(new[] { 0, 5 }, latchFrames);
    }

    private sealed class RecordingInput : IKeyboardTextInput
    {
        public string Pressed { get; set; } = "";
        public bool CanPressCharacter(char character) => character < 128;
        public void PressCharacter(char character) => Pressed += character;
    }

    private sealed class EmptyHostInput : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; } = new HashSet<HostKey>();
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();
        public bool CapsLockOn => false;
        public void UpdatePerFrame() { }
    }
}
