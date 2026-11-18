namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

[Flags]
internal enum C64KeyboardFlags : byte
{
    None = 0,
    RestoreHeld = 1,
    CapsLock = 2,
    RunStopHeld = 4,
    RestoreEdge = 8,
}

public partial class C64Keyboard
{
    // Keys have already passed through shift lock and keyboard-to-joystick mapping.
    internal (ulong Keys, C64KeyboardFlags Flags) CaptureResolvedKeys()
    {
        ulong keys = 0;
        foreach (var key in _pressedKeys)
            if (key != C64Key.None)
                keys |= 1UL << ((int)key - 1);

        var flags = C64KeyboardFlags.None;
        if (_restorePressedLastFrame) flags |= C64KeyboardFlags.RestoreHeld;
        if (_capsLockOn) flags |= C64KeyboardFlags.CapsLock;
        if (_runStopPressedLastFrame) flags |= C64KeyboardFlags.RunStopHeld;
        if (_restorePulseThisFrame) flags |= C64KeyboardFlags.RestoreEdge;
        return (keys, flags);
    }

    internal void RestoreResolvedKeys(ulong keys, C64KeyboardFlags flags)
    {
        _pressedKeys.Clear();
        for (int key = 1; key <= (int)C64Key.F7; key++)
            if ((keys & (1UL << (key - 1))) != 0)
                _pressedKeys.Add((C64Key)key);

        _restorePressedLastFrame = (flags & C64KeyboardFlags.RestoreHeld) != 0;
        _capsLockOn = (flags & C64KeyboardFlags.CapsLock) != 0;
        _runStopPressedLastFrame = (flags & C64KeyboardFlags.RunStopHeld) != 0;
        _restorePulseThisFrame = (flags & C64KeyboardFlags.RestoreEdge) != 0;
    }

    // During playback each recorded RESTORE edge must reach the CPU exactly once.
    // Snapshot restore already installs the CPU's NMI state from the same instant.
    internal void ApplyRecordedRestoreLine()
    {
        if (_restorePulseThisFrame)
            PulseRestoreKeyPressed();
        else if (!_restorePressedLastFrame)
            _c64.CPU.CPUInterrupts.SetNMISourceInactive(RestoreNmiSource);
    }
}
