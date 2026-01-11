using System.Collections.Generic;
using Avalonia.Input;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Impl.Avalonia.Input;

public class AvaloniaInputHandlerContext : IInputHandlerContext
{
    public bool IsInitialized { get; private set; } = false;

    // Keyboard state tracking
    public HashSet<Key> KeysDown = new();
    private bool _capsLockOn;

    // Gamepad state tracking
    private readonly IGamepad _gamepad;

    /// <summary>
    /// Gets the gamepad provider.
    /// </summary>
    public IGamepad Gamepad => _gamepad;

    /// <summary>
    /// Gets the set of currently pressed gamepad buttons.
    /// Convenience property that delegates to the gamepad provider.
    /// </summary>
    public HashSet<GamepadButton> GamepadButtonsDown => _gamepad.ButtonsDown;

    /// <summary>
    /// Creates a new AvaloniaInputHandlerContext with a null gamepad (no gamepad support).
    /// </summary>
    public AvaloniaInputHandlerContext() : this(new NullGamepad())
    {
    }

    /// <summary>
    /// Creates a new AvaloniaInputHandlerContext with the specified gamepad provider.
    /// </summary>
    /// <param name="gamepad">The gamepad provider to use. Pass null to use a NullAvaloniaGamepad.</param>
    public AvaloniaInputHandlerContext(IGamepad? gamepad)
    {
        _gamepad = gamepad ?? new NullGamepad();
    }

    public bool GetCapsLockState() => _capsLockOn;

    public void SetCapsLockState(bool capsLockOn) => _capsLockOn = capsLockOn;

    public void AddKeyDown(Key key)
    {
        KeysDown.Add(key);
        if (key == Key.CapsLock)
            _capsLockOn = !_capsLockOn;
    }

    public void RemoveKeyDown(Key key)
    {
        KeysDown.Remove(key);
    }

    public void ClearKeysDown()
    {
        KeysDown.Clear();
    }

    /// <summary>
    /// Updates gamepad state. Should be called once per frame.
    /// </summary>
    public void UpdateGamepad()
    {
        _gamepad.Update();
    }

    public void Init()
    {
        IsInitialized = true;
        KeysDown.Clear();
        _capsLockOn = false;
        _gamepad.Init();
    }

    public void Cleanup()
    {
        IsInitialized = false;
        KeysDown.Clear();
        _gamepad.Cleanup();
    }
}
