using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Systems.Vic20.Input;

/// <summary>Keyboard-matrix injection shared by native text input, scripting and remoting.</summary>
public sealed class Vic20InputInjector(Vic20 vic20) : IInputInjector, IKeyboardTextInput
{
    private readonly HashSet<Vic20Key> _frameKeys = [];
    private readonly HashSet<Vic20Key> _heldKeys = [];
    private static readonly Dictionary<string, Vic20Key> Keys = CreateKeys();

    private static Dictionary<string, Vic20Key> CreateKeys()
    {
        var keys = new Dictionary<string, Vic20Key>(StringComparer.OrdinalIgnoreCase)
        {
            ["space"] = Vic20Key.Space, ["return"] = Vic20Key.Return,
            ["delete"] = Vic20Key.Delete, ["stop"] = Vic20Key.RunStop,
            ["lshift"] = Vic20Key.LShift, ["rshift"] = Vic20Key.RShift,
            ["ctrl"] = Vic20Key.Ctrl, ["cbm"] = Vic20Key.CBM, ["home"] = Vic20Key.Home,
            ["crsrdown"] = Vic20Key.CrsrDown, ["crsrright"] = Vic20Key.CrsrRight,
            ["f1"] = Vic20Key.F1, ["f3"] = Vic20Key.F3, ["f5"] = Vic20Key.F5, ["f7"] = Vic20Key.F7,
            ["+"] = Vic20Key.Plus, ["-"] = Vic20Key.Minus, ["*"] = Vic20Key.Asterisk,
            ["/"] = Vic20Key.Slash, [":"] = Vic20Key.Colon, [";"] = Vic20Key.Semicolon,
            ["="] = Vic20Key.Equal, ["."] = Vic20Key.Period, [","] = Vic20Key.Comma,
            ["@"] = Vic20Key.At, ["lira"] = Vic20Key.Pound,
            ["leftarrow"] = Vic20Key.BackArrow, ["rightarrow"] = Vic20Key.UpArrow,
        };
        for (var letter = 'a'; letter <= 'z'; letter++)
            keys[letter.ToString()] = Vic20Key.A + (letter - 'a');
        for (var digit = '0'; digit <= '9'; digit++)
            keys[digit.ToString()] = Vic20Key.Zero + (digit - '0');
        return keys;
    }

    public IReadOnlyList<string> GetAvailableKeys() => [.. Keys.Keys];
    public void BeginFrame() => _frameKeys.Clear();
    public void KeyReleaseAll() => _frameKeys.Clear();
    public void ReleaseAllHeldKeys() => _heldKeys.Clear();
    public void KeyPress(string keyName) => AddKey(_frameKeys, keyName);
    public void HoldKey(string keyName) => AddKey(_heldKeys, keyName);
    public void KeyRelease(string keyName) => RemoveKey(_frameKeys, keyName);
    public void ReleaseHeldKey(string keyName) => RemoveKey(_heldKeys, keyName);
    public bool IsKeyDown(string keyName) => Keys.TryGetValue(keyName, out var key)
        && (_frameKeys.Contains(key) || _heldKeys.Contains(key) || vic20.Via1.Keyboard.IsKeyPressed(key));

    private static void AddKey(HashSet<Vic20Key> keys, string name)
    {
        if (Keys.TryGetValue(name, out var key)) keys.Add(key);
    }

    private static void RemoveKey(HashSet<Vic20Key> keys, string name)
    {
        if (Keys.TryGetValue(name, out var key)) keys.Remove(key);
    }

    public bool CanPressCharacter(char character) => KeyboardTextMapping.CommodoreKeys(character) != null;
    public void PressCharacter(char character)
    {
        foreach (var key in KeyboardTextMapping.CommodoreKeys(character) ?? [])
            KeyPress(key);
    }

    public void ApplyInjectedKeysTo(List<Vic20Key> keys)
    {
        foreach (var key in _heldKeys.Concat(_frameKeys))
            if (!keys.Contains(key)) keys.Add(key);
    }

    public void Clear()
    {
        BeginFrame();
        ReleaseAllHeldKeys();
    }

    // No joystick injection is provided by the VIC-20 input consumer.
    public int JoystickPortCount => 0;
    public IReadOnlyList<string> GetAvailableJoystickActions() => [];
    public bool IsJoystickActionDown(int port, string actionName) => false;
    public void SetJoystickAction(int port, string actionName, bool pressed) { /* VIC-20 joystick injection is not supported. */ }
    public void HoldJoystickAction(int port, string actionName) { /* VIC-20 joystick injection is not supported. */ }
    public void ReleaseHeldJoystickAction(int port, string actionName) { /* VIC-20 joystick injection is not supported. */ }
    public void ReleaseAllHeldJoystickActions(int port) { /* VIC-20 joystick injection is not supported. */ }
}
