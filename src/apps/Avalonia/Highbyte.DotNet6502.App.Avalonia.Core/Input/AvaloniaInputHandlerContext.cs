using System.Collections.Generic;
using Avalonia.Input;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Input;

public class AvaloniaInputHandlerContext : IInputHandlerContext
{
    public bool IsInitialized { get; private set; } = false;

    // Keyboard state tracking
    public HashSet<Key> KeysDown = new();
    private bool _capsLockOn;

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

    public void Init()
    {
        IsInitialized = true;
        KeysDown.Clear();
        _capsLockOn = false;
    }

    public void Cleanup()
    {
        IsInitialized = false;
        KeysDown.Clear();
    }
}
