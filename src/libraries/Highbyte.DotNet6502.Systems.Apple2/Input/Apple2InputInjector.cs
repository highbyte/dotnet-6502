using Highbyte.DotNet6502.Systems.Input;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Input;

/// <summary>
/// Remote-control input injector for the Apple II.
///
/// Injected key names resolve to <see cref="HostKey"/> values that are merged into the host's
/// keys-down set each frame, so injected input flows through the same edge-detection,
/// auto-repeat, and ASCII mapping in <see cref="Apple2InputHandler"/> as real keyboard input.
/// A <c>KeyPress</c> lasts one frame (one latch write); <c>HoldKey</c> keeps the key down until
/// released, which also engages the handler's typematic auto-repeat. "shift" and "ctrl" act as
/// modifiers combining with other injected keys, exactly like the physical keyboard.
///
/// The machine has no per-key state (only the single ASCII latch), so <see cref="IsKeyDown"/>
/// reflects injected state only.
///
/// Joystick actions are injected the same way and merged into the handler's action set, so they
/// reach the analog game port through exactly the path a gamepad takes. There is one game port,
/// so the port argument is accepted and ignored rather than rejected — scripts written against
/// the C64's two ports keep working.
/// </summary>
public class Apple2InputInjector : IInputInjector
{
    private readonly Apple2System _apple2;

    private readonly HashSet<HostKey> _frameInjectedKeys = new();
    private readonly HashSet<HostKey> _heldKeys = new();

    private readonly HashSet<JoystickAction> _frameInjectedJoystickActions = new();
    private readonly HashSet<JoystickAction> _heldJoystickActions = new();

    private static readonly Dictionary<string, JoystickAction> StringToJoystickAction =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["up"] = JoystickAction.Up,
            ["down"] = JoystickAction.Down,
            ["left"] = JoystickAction.Left,
            ["right"] = JoystickAction.Right,
            ["fire"] = JoystickAction.Fire,
            ["fire2"] = JoystickAction.Fire2,
        };

    private static readonly Dictionary<string, HostKey> StringToHostKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = HostKey.KeyA,
        ["b"] = HostKey.KeyB,
        ["c"] = HostKey.KeyC,
        ["d"] = HostKey.KeyD,
        ["e"] = HostKey.KeyE,
        ["f"] = HostKey.KeyF,
        ["g"] = HostKey.KeyG,
        ["h"] = HostKey.KeyH,
        ["i"] = HostKey.KeyI,
        ["j"] = HostKey.KeyJ,
        ["k"] = HostKey.KeyK,
        ["l"] = HostKey.KeyL,
        ["m"] = HostKey.KeyM,
        ["n"] = HostKey.KeyN,
        ["o"] = HostKey.KeyO,
        ["p"] = HostKey.KeyP,
        ["q"] = HostKey.KeyQ,
        ["r"] = HostKey.KeyR,
        ["s"] = HostKey.KeyS,
        ["t"] = HostKey.KeyT,
        ["u"] = HostKey.KeyU,
        ["v"] = HostKey.KeyV,
        ["w"] = HostKey.KeyW,
        ["x"] = HostKey.KeyX,
        ["y"] = HostKey.KeyY,
        ["z"] = HostKey.KeyZ,
        ["0"] = HostKey.Digit0,
        ["1"] = HostKey.Digit1,
        ["2"] = HostKey.Digit2,
        ["3"] = HostKey.Digit3,
        ["4"] = HostKey.Digit4,
        ["5"] = HostKey.Digit5,
        ["6"] = HostKey.Digit6,
        ["7"] = HostKey.Digit7,
        ["8"] = HostKey.Digit8,
        ["9"] = HostKey.Digit9,
        ["space"] = HostKey.Space,
        ["return"] = HostKey.Enter,
        ["tab"] = HostKey.Tab,
        ["esc"] = HostKey.Escape,
        ["backspace"] = HostKey.Backspace,
        ["left"] = HostKey.ArrowLeft,
        ["right"] = HostKey.ArrowRight,
        ["up"] = HostKey.ArrowUp,
        ["down"] = HostKey.ArrowDown,
        ["-"] = HostKey.Minus,
        ["="] = HostKey.Equal,
        ["["] = HostKey.BracketLeft,
        ["]"] = HostKey.BracketRight,
        ["\\"] = HostKey.Backslash,
        [";"] = HostKey.Semicolon,
        ["'"] = HostKey.Quote,
        [","] = HostKey.Comma,
        ["."] = HostKey.Period,
        ["/"] = HostKey.Slash,
        ["`"] = HostKey.Backquote,
        ["shift"] = HostKey.ShiftLeft,
        ["ctrl"] = HostKey.ControlLeft,
    };

    public Apple2InputInjector(Apple2System apple2)
    {
        _apple2 = apple2;
    }

    /// <summary>Whether any injected keys are active, so callers can skip merge work.</summary>
    public bool HasInjectedKeys => _frameInjectedKeys.Count > 0 || _heldKeys.Count > 0;

    public IReadOnlyList<string> GetAvailableKeys()
    {
        return StringToHostKey.Keys.ToList();
    }

    public IReadOnlyList<string> GetAvailableJoystickActions()
    {
        return StringToJoystickAction.Keys.ToList();
    }

    /// <summary>One game port, so one joystick.</summary>
    public int JoystickPortCount => 1;

    public bool HasInjectedJoystickActions
        => _frameInjectedJoystickActions.Count > 0 || _heldJoystickActions.Count > 0;

    public void BeginFrame()
    {
        _frameInjectedKeys.Clear();
    }

    public void KeyPress(string keyName)
    {
        if (StringToHostKey.TryGetValue(keyName, out var hostKey))
            _frameInjectedKeys.Add(hostKey);
    }

    public void KeyRelease(string keyName)
    {
        if (StringToHostKey.TryGetValue(keyName, out var hostKey))
            _frameInjectedKeys.Remove(hostKey);
    }

    public void KeyReleaseAll()
    {
        _frameInjectedKeys.Clear();
    }

    public void HoldKey(string keyName)
    {
        if (StringToHostKey.TryGetValue(keyName, out var hostKey))
            _heldKeys.Add(hostKey);
    }

    public void ReleaseHeldKey(string keyName)
    {
        if (StringToHostKey.TryGetValue(keyName, out var hostKey))
            _heldKeys.Remove(hostKey);
    }

    public void ReleaseAllHeldKeys()
    {
        _heldKeys.Clear();
    }

    public bool IsKeyDown(string keyName)
    {
        if (!StringToHostKey.TryGetValue(keyName, out var hostKey))
            return false;
        return _heldKeys.Contains(hostKey) || _frameInjectedKeys.Contains(hostKey);
    }

    public void SetJoystickAction(int port, string actionName, bool pressed)
    {
        if (!StringToJoystickAction.TryGetValue(actionName, out var action))
            return;

        if (pressed)
            _frameInjectedJoystickActions.Add(action);
        else
        {
            _frameInjectedJoystickActions.Remove(action);
            _heldJoystickActions.Remove(action);
        }
    }

    public void HoldJoystickAction(int port, string actionName)
    {
        if (StringToJoystickAction.TryGetValue(actionName, out var action))
            _heldJoystickActions.Add(action);
    }

    public void ReleaseHeldJoystickAction(int port, string actionName)
    {
        if (StringToJoystickAction.TryGetValue(actionName, out var action))
            _heldJoystickActions.Remove(action);
    }

    public void ReleaseAllHeldJoystickActions(int port) => _heldJoystickActions.Clear();

    public bool IsJoystickActionDown(int port, string actionName)
        => StringToJoystickAction.TryGetValue(actionName, out var action)
           && (_heldJoystickActions.Contains(action) || _frameInjectedJoystickActions.Contains(action));

    /// <summary>
    /// Merges injected joystick actions into the set the handler is about to apply to the game
    /// port. Held actions persist; one-frame ones are consumed by the handler clearing them.
    /// </summary>
    public void ApplyInjectedJoystickActionsTo(HashSet<JoystickAction> actions)
    {
        foreach (var action in _heldJoystickActions)
            actions.Add(action);

        foreach (var action in _frameInjectedJoystickActions)
            actions.Add(action);
    }

    /// <summary>Drops the one-frame joystick actions, mirroring how injected keys are consumed.</summary>
    public void ClearFrameInjectedJoystickActions() => _frameInjectedJoystickActions.Clear();

    public void Clear()
    {
        _heldKeys.Clear();
        BeginFrame();
    }

    /// <summary>Merges injected keys into the host keys-down set for this frame.</summary>
    public void ApplyInjectedKeysTo(HashSet<HostKey> hostKeysDown)
    {
        foreach (var key in _heldKeys)
            hostKeysDown.Add(key);

        foreach (var key in _frameInjectedKeys)
            hostKeysDown.Add(key);
    }
}
