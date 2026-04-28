using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Systems.Commodore64;

public class C64InputInjector : IInputInjector
{
    private readonly C64 _c64;

    private readonly HashSet<C64Key> _frameInjectedKeys = new();
    private readonly HashSet<C64Key> _heldKeys = new();
    private readonly Dictionary<int, HashSet<C64JoystickAction>> _heldJoystickActions = new()
    {
        { 1, new HashSet<C64JoystickAction>() },
        { 2, new HashSet<C64JoystickAction>() }
    };
    private readonly Dictionary<int, HashSet<C64JoystickAction>> _frameInjectedJoystickActions = new()
    {
        { 1, new HashSet<C64JoystickAction>() },
        { 2, new HashSet<C64JoystickAction>() }
    };

    private static readonly Dictionary<string, C64Key> StringToC64Key = new(StringComparer.OrdinalIgnoreCase)
    {
        ["space"]    = C64Key.Space,
        ["a"]        = C64Key.A,
        ["b"]        = C64Key.B,
        ["c"]        = C64Key.C,
        ["d"]        = C64Key.D,
        ["e"]        = C64Key.E,
        ["f"]        = C64Key.F,
        ["g"]        = C64Key.G,
        ["h"]        = C64Key.H,
        ["i"]        = C64Key.I,
        ["j"]        = C64Key.J,
        ["k"]        = C64Key.K,
        ["l"]        = C64Key.L,
        ["m"]        = C64Key.M,
        ["n"]        = C64Key.N,
        ["o"]        = C64Key.O,
        ["p"]        = C64Key.P,
        ["q"]        = C64Key.Q,
        ["r"]        = C64Key.R,
        ["s"]        = C64Key.S,
        ["t"]        = C64Key.T,
        ["u"]        = C64Key.U,
        ["v"]        = C64Key.V,
        ["w"]        = C64Key.W,
        ["x"]        = C64Key.X,
        ["y"]        = C64Key.Y,
        ["z"]        = C64Key.Z,
        ["0"]        = C64Key.Zero,
        ["1"]        = C64Key.One,
        ["2"]        = C64Key.Two,
        ["3"]        = C64Key.Three,
        ["4"]        = C64Key.Four,
        ["5"]        = C64Key.Five,
        ["6"]        = C64Key.Six,
        ["7"]        = C64Key.Seven,
        ["8"]        = C64Key.Eight,
        ["9"]        = C64Key.Nine,
        ["+"]        = C64Key.Plus,
        ["-"]        = C64Key.Minus,
        ["*"]        = C64Key.Astrix,
        ["/"]        = C64Key.Slash,
        [":"]        = C64Key.Colon,
        [";"]        = C64Key.Semicol,
        ["="]        = C64Key.Equal,
        ["."]        = C64Key.Period,
        [","]        = C64Key.Comma,
        ["@"]        = C64Key.At,
        ["lira"]     = C64Key.Lira,
        ["leftarrow"] = C64Key.LArrow,
        ["rightarrow"] = C64Key.UArrow,
        ["stop"]     = C64Key.Stop,
        ["cbm"]      = C64Key.CBM,
        ["ctrl"]     = C64Key.Ctrl,
        ["rshift"]   = C64Key.RShift,
        ["home"]     = C64Key.Home,
        ["lshift"]   = C64Key.LShift,
        ["crsrdown"] = C64Key.CrsrDn,
        ["crsrright"] = C64Key.CrsrRt,
        ["return"]   = C64Key.Return,
        ["delete"]   = C64Key.Delete,
        ["f1"]       = C64Key.F1,
        ["f3"]       = C64Key.F3,
        ["f5"]       = C64Key.F5,
        ["f7"]       = C64Key.F7,
    };

    private static readonly Dictionary<string, C64JoystickAction> StringToC64JoystickAction = new(StringComparer.OrdinalIgnoreCase)
    {
        ["up"]    = C64JoystickAction.Up,
        ["down"]  = C64JoystickAction.Down,
        ["left"]  = C64JoystickAction.Left,
        ["right"] = C64JoystickAction.Right,
        ["fire"]  = C64JoystickAction.Fire,
    };

    public C64InputInjector(C64 c64)
    {
        _c64 = c64;
    }

    public IReadOnlyList<string> GetAvailableKeys()
    {
        return StringToC64Key.Keys.ToList();
    }

    public IReadOnlyList<string> GetAvailableJoystickActions()
    {
        return new List<string> { "up", "down", "left", "right", "fire" };
    }

    public int JoystickPortCount => 2;

    public void BeginFrame()
    {
        _frameInjectedKeys.Clear();
        _frameInjectedJoystickActions[1].Clear();
        _frameInjectedJoystickActions[2].Clear();
    }

    public void KeyPress(string keyName)
    {
        if (StringToC64Key.TryGetValue(keyName, out var c64Key))
            _frameInjectedKeys.Add(c64Key);
    }

    public void KeyRelease(string keyName)
    {
        if (StringToC64Key.TryGetValue(keyName, out var c64Key))
            _frameInjectedKeys.Remove(c64Key);
    }

    public void KeyReleaseAll()
    {
        _frameInjectedKeys.Clear();
    }

    public void HoldKey(string keyName)
    {
        if (StringToC64Key.TryGetValue(keyName, out var c64Key))
            _heldKeys.Add(c64Key);
    }

    public void ReleaseHeldKey(string keyName)
    {
        if (StringToC64Key.TryGetValue(keyName, out var c64Key))
            _heldKeys.Remove(c64Key);
    }

    public void ReleaseAllHeldKeys()
    {
        _heldKeys.Clear();
    }

    public bool IsKeyDown(string keyName)
    {
        if (!StringToC64Key.TryGetValue(keyName, out var c64Key))
            return false;
        return _c64.Cia1.Keyboard.IsKeyCurrentlyPressed(c64Key)
            || _heldKeys.Contains(c64Key)
            || _frameInjectedKeys.Contains(c64Key);
    }

    public void SetJoystickAction(int port, string actionName, bool pressed)
    {
        if (!TryGetJoystickAction(port, actionName, out var action)) return;

        if (pressed)
            _frameInjectedJoystickActions[port].Add(action);
        else
            _frameInjectedJoystickActions[port].Remove(action);
    }

    public void HoldJoystickAction(int port, string actionName)
    {
        if (!TryGetJoystickAction(port, actionName, out var action)) return;
        _heldJoystickActions[port].Add(action);
    }

    public void ReleaseHeldJoystickAction(int port, string actionName)
    {
        if (!TryGetJoystickAction(port, actionName, out var action)) return;
        _heldJoystickActions[port].Remove(action);
    }

    public void ReleaseAllHeldJoystickActions(int port)
    {
        if (port < 1 || port > 2) return;
        _heldJoystickActions[port].Clear();
    }

    public bool IsJoystickActionDown(int port, string actionName)
    {
        if (!TryGetJoystickAction(port, actionName, out var action)) return false;

        var realActions = _c64.Cia1.Joystick.CurrentJoystickActions;
        return (realActions.TryGetValue(port, out var set) && set.Contains(action))
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

    public void ApplyInjectedKeysTo(List<C64Key> c64KeysDown)
    {
        foreach (var key in _heldKeys)
        {
            if (!c64KeysDown.Contains(key))
                c64KeysDown.Add(key);
        }

        foreach (var key in _frameInjectedKeys)
        {
            if (!c64KeysDown.Contains(key))
                c64KeysDown.Add(key);
        }
    }

    public void ApplyInjectedJoystickActionsTo(C64Joystick joystick)
    {
        for (int port = 1; port <= 2; port++)
        {
            if (_heldJoystickActions[port].Count > 0)
                joystick.SetJoystickActions(port, _heldJoystickActions[port], overwrite: false);

            if (_frameInjectedJoystickActions[port].Count > 0)
                joystick.SetJoystickActions(port, _frameInjectedJoystickActions[port], overwrite: false);
        }
    }

    private static bool TryGetJoystickAction(int port, string actionName, out C64JoystickAction action)
    {
        action = default;
        if (port < 1 || port > 2) return false;
        return StringToC64JoystickAction.TryGetValue(actionName, out action);
    }
}
