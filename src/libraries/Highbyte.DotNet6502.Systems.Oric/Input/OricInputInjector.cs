using Highbyte.DotNet6502.Systems.Input;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Input;

/// <summary>Bridges remote-control input commands to the Oric keyboard and joystick state.</summary>
public sealed class OricInputInjector : IInputInjector
{
    private readonly OricMachine _oric;
    private readonly HashSet<HostKey> _frameInjectedKeys = [];
    private readonly HashSet<HostKey> _heldKeys = [];
    private readonly Dictionary<int, HashSet<JoystickAction>> _heldJoystickActions = new()
    {
        [1] = [],
        [2] = [],
    };
    private readonly Dictionary<int, HashSet<JoystickAction>> _frameInjectedJoystickActions = new()
    {
        [1] = [],
        [2] = [],
    };

    private static readonly IReadOnlyDictionary<string, HostKey> s_stringToHostKey =
        new Dictionary<string, HostKey>(StringComparer.OrdinalIgnoreCase)
        {
            ["space"] = HostKey.Space,
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
            ["return"] = HostKey.Enter,
            ["backspace"] = HostKey.Backspace,
            ["esc"] = HostKey.Escape,
            ["left"] = HostKey.ArrowLeft,
            ["right"] = HostKey.ArrowRight,
            ["up"] = HostKey.ArrowUp,
            ["down"] = HostKey.ArrowDown,
            ["shift"] = HostKey.ShiftLeft,
            ["lshift"] = HostKey.ShiftLeft,
            ["rshift"] = HostKey.ShiftRight,
            ["ctrl"] = HostKey.ControlLeft,
            ["lctrl"] = HostKey.ControlLeft,
            ["rctrl"] = HostKey.ControlRight,
            ["funct"] = HostKey.AltLeft,
            ["alt"] = HostKey.AltLeft,
        };

    private static readonly IReadOnlyDictionary<string, JoystickAction> s_stringToJoystickAction =
        new Dictionary<string, JoystickAction>(StringComparer.OrdinalIgnoreCase)
        {
            ["up"] = JoystickAction.Up,
            ["down"] = JoystickAction.Down,
            ["left"] = JoystickAction.Left,
            ["right"] = JoystickAction.Right,
            ["fire"] = JoystickAction.Fire,
        };

    public OricInputInjector(OricMachine oric) => _oric = oric;

    public int JoystickPortCount => 2;

    public bool HasInjectedKeys => _heldKeys.Count > 0 || _frameInjectedKeys.Count > 0;

    public IReadOnlyList<string> GetAvailableKeys() => [.. s_stringToHostKey.Keys];

    public IReadOnlyList<string> GetAvailableJoystickActions() => [.. s_stringToJoystickAction.Keys];

    public void BeginFrame()
    {
        _frameInjectedKeys.Clear();
        _frameInjectedJoystickActions[1].Clear();
        _frameInjectedJoystickActions[2].Clear();
    }

    public void KeyPress(string keyName)
    {
        if (s_stringToHostKey.TryGetValue(keyName, out var key))
            _frameInjectedKeys.Add(key);
    }

    public void KeyRelease(string keyName)
    {
        if (s_stringToHostKey.TryGetValue(keyName, out var key))
            _frameInjectedKeys.Remove(key);
    }

    public void KeyReleaseAll() => _frameInjectedKeys.Clear();

    public void HoldKey(string keyName)
    {
        if (s_stringToHostKey.TryGetValue(keyName, out var key))
            _heldKeys.Add(key);
    }

    public void ReleaseHeldKey(string keyName)
    {
        if (s_stringToHostKey.TryGetValue(keyName, out var key))
            _heldKeys.Remove(key);
    }

    public void ReleaseAllHeldKeys() => _heldKeys.Clear();

    public bool IsKeyDown(string keyName)
        => s_stringToHostKey.TryGetValue(keyName, out var key)
           && (_oric.Keyboard.IsKeyPressed(key) || _heldKeys.Contains(key) || _frameInjectedKeys.Contains(key));

    public void SetJoystickAction(int port, string actionName, bool pressed)
    {
        if (!TryGetJoystickAction(port, actionName, out var action))
            return;

        if (pressed)
            _frameInjectedJoystickActions[port].Add(action);
        else
            _frameInjectedJoystickActions[port].Remove(action);
    }

    public void HoldJoystickAction(int port, string actionName)
    {
        if (TryGetJoystickAction(port, actionName, out var action))
            _heldJoystickActions[port].Add(action);
    }

    public void ReleaseHeldJoystickAction(int port, string actionName)
    {
        if (TryGetJoystickAction(port, actionName, out var action))
            _heldJoystickActions[port].Remove(action);
    }

    public void ReleaseAllHeldJoystickActions(int port)
    {
        if (port is 1 or 2)
            _heldJoystickActions[port].Clear();
    }

    public bool IsJoystickActionDown(int port, string actionName)
    {
        if (!TryGetJoystickAction(port, actionName, out var action))
            return false;

        return (_oric.Joystick.CurrentJoystickActions.TryGetValue(port, out var actions) && actions.Contains(action))
               || _heldJoystickActions[port].Contains(action)
               || _frameInjectedJoystickActions[port].Contains(action);
    }

    public void Clear()
    {
        _heldKeys.Clear();
        _heldJoystickActions[1].Clear();
        _heldJoystickActions[2].Clear();
        BeginFrame();
    }

    public void ApplyInjectedKeysTo(HashSet<HostKey> keysDown)
    {
        keysDown.UnionWith(_heldKeys);
        keysDown.UnionWith(_frameInjectedKeys);
    }

    public void ApplyInjectedJoystickActionsTo(OricJoystick joystick)
    {
        for (var port = 1; port <= 2; port++)
        {
            joystick.SetJoystickActions(port, _heldJoystickActions[port], overwrite: false);
            joystick.SetJoystickActions(port, _frameInjectedJoystickActions[port], overwrite: false);
        }
    }

    private static bool TryGetJoystickAction(int port, string actionName, out JoystickAction action)
    {
        action = default;
        return port is 1 or 2 && s_stringToJoystickAction.TryGetValue(actionName, out action);
    }
}
