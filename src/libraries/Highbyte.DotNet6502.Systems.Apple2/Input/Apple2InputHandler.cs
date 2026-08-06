using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Input;

/// <summary>
/// Host-agnostic Apple II input handler.
///
/// The emulated machine has no keyboard matrix and no interrupt-driven scan: the encoder simply
/// latches the ASCII code of the most recent key press and raises a strobe that software polls.
/// This handler therefore converts <em>edges</em> — a key that was not held on the previous
/// frame — into a single latch write, plus a typematic auto-repeat while the key stays down so
/// holding a key behaves the way users expect from a modern keyboard.
/// </summary>
public class Apple2InputHandler : IInputConsumer
{
    /// <summary>Frames a key must be held before auto-repeat starts (~0.5s at 60 Hz).</summary>
    public const int AutoRepeatDelayFrames = 30;

    /// <summary>Frames between repeats once auto-repeat has started (~15 chars/s at 60 Hz).</summary>
    public const int AutoRepeatIntervalFrames = 4;

    private readonly Apple2System _apple2;
    private readonly ILogger _logger;

    private IHostInputState _inputState = default!;

    private readonly HashSet<HostKey> _previousKeysDown = new();
    private HostKey _repeatingKey = HostKey.None;
    private int _framesHeld;

    public ISystem System => _apple2;
    public Instrumentations Instrumentations { get; } = new();

    public Apple2InputHandler(Apple2System apple2, ILoggerFactory loggerFactory)
    {
        _apple2 = apple2;
        _logger = loggerFactory.CreateLogger(nameof(Apple2InputHandler));
    }

    public void Init(IHostInputState inputState)
    {
        _inputState = inputState;
    }

    public void BeforeFrame()
    {
        _inputState.UpdatePerFrame();
        IReadOnlySet<HostKey> keysDown = _inputState.KeysDown;

        // Merge remotely injected keys so they flow through the same edge detection,
        // auto-repeat, and ASCII mapping as real keyboard input.
        if (_apple2.InputInjector.HasInjectedKeys)
        {
            var merged = new HashSet<HostKey>(keysDown);
            _apple2.InputInjector.ApplyInjectedKeysTo(merged);
            keysDown = merged;
        }

        var shift = keysDown.Contains(HostKey.ShiftLeft) || keysDown.Contains(HostKey.ShiftRight);
        var control = keysDown.Contains(HostKey.ControlLeft) || keysDown.Contains(HostKey.ControlRight);

        // CTRL-RESET. The RESET key is wired to the 6502 /RES line (not the keyboard encoder),
        // with CTRL required on later II Plus keyboards. Mapped to Ctrl+F12, the same host combo
        // Virtual ][ uses. Edge-triggered so holding it does not reset every frame.
        if (control && keysDown.Contains(HostKey.F12) && !_previousKeysDown.Contains(HostKey.F12))
        {
            _apple2.Reset();
            _logger.LogDebug("Apple II input: CTRL-RESET (Ctrl+F12) — warm reset via the reset vector.");
        }

        var key = ResolveKeyToLatch(keysDown);
        if (key != HostKey.None && Apple2HostKeyboard.TryGetAscii(key, shift, control, out var ascii))
        {
            _apple2.Keyboard.KeyPressed(ascii);
            _logger.LogTrace("Apple II input: host={HostKey} ascii=${Ascii:X2}", key, ascii);
        }

        _previousKeysDown.Clear();
        foreach (var k in keysDown)
            _previousKeysDown.Add(k);
    }

    public void Cleanup() { }

    public List<string> GetDebugInfo() => new()
    {
        $"Keyboard latch: ${_apple2.Keyboard.Latch:X2} (strobe {(_apple2.Keyboard.StrobeSet ? "set" : "clear")})",
    };

    /// <summary>
    /// Picks the key whose code should be latched this frame: a newly pressed key wins, and
    /// otherwise a still-held key repeats once its delay has elapsed.
    /// </summary>
    private HostKey ResolveKeyToLatch(IReadOnlySet<HostKey> keysDown)
    {
        var newlyPressed = HostKey.None;
        foreach (var key in keysDown)
        {
            if (Apple2HostKeyboard.ModifierKeys.Contains(key))
                continue;
            if (!Apple2HostKeyboard.HostKeyToAsciiMap.ContainsKey(key))
                continue;
            if (_previousKeysDown.Contains(key))
                continue;
            // Deterministic pick when several keys go down in the same frame.
            if (newlyPressed == HostKey.None || key < newlyPressed)
                newlyPressed = key;
        }

        if (newlyPressed != HostKey.None)
        {
            _repeatingKey = newlyPressed;
            _framesHeld = 0;
            return newlyPressed;
        }

        if (_repeatingKey == HostKey.None || !keysDown.Contains(_repeatingKey))
        {
            _repeatingKey = HostKey.None;
            _framesHeld = 0;
            return HostKey.None;
        }

        _framesHeld++;
        if (_framesHeld < AutoRepeatDelayFrames)
            return HostKey.None;

        return (_framesHeld - AutoRepeatDelayFrames) % AutoRepeatIntervalFrames == 0
            ? _repeatingKey
            : HostKey.None;
    }
}
