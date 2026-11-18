namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>Suppresses controls held across replay exit until they have been released.</summary>
internal sealed class FreshHostInputState(IHostInputState source) : IHostInputState
{
    private readonly HashSet<HostKey> _blockedKeys = [];
    private readonly HashSet<GamepadButton> _blockedButtons = [];
    private readonly HashSet<HostKey> _keys = [];
    private readonly HashSet<GamepadButton> _buttons = [];
    private bool _blockCapsLock;
    public IReadOnlySet<HostKey> KeysDown => _blockedKeys.Count == 0 ? source.KeysDown : _keys;
    public IReadOnlySet<GamepadButton> GamepadButtonsDown => _blockedButtons.Count == 0 ? source.GamepadButtonsDown : _buttons;
    public bool CapsLockOn => !_blockCapsLock && source.CapsLockOn;
    public bool IsRunningOnMacOS => source.IsRunningOnMacOS;
    public string? DetectNativeKeyboardLayoutId() => source.DetectNativeKeyboardLayoutId();

    public void RequireFreshInput()
    {
        source.UpdatePerFrame();
        _blockedKeys.UnionWith(source.KeysDown);
        _blockedButtons.UnionWith(source.GamepadButtonsDown);
        _blockCapsLock = source.CapsLockOn;
        _keys.Clear();
        _buttons.Clear();
    }

    public void UpdatePerFrame()
    {
        source.UpdatePerFrame();
        // Only the first frames after replay can have held controls to filter.
        if (_blockedKeys.Count != 0)
            FilterHeldControls(source.KeysDown, _blockedKeys, _keys);
        if (_blockedButtons.Count != 0)
            FilterHeldControls(source.GamepadButtonsDown, _blockedButtons, _buttons);
        if (_blockCapsLock)
            _blockCapsLock = source.CapsLockOn;
    }

    private static void FilterHeldControls<T>(IReadOnlySet<T> down, HashSet<T> blocked, HashSet<T> visible)
    {
        blocked.IntersectWith(down);
        visible.Clear();
        if (blocked.Count == 0) return;
        visible.UnionWith(down);
        visible.ExceptWith(blocked);
    }
}

public sealed class EmptyHostInputState : IHostInputState
{
    private static readonly HashSet<HostKey> NoKeys = [];
    private static readonly HashSet<GamepadButton> NoButtons = [];
    public IReadOnlySet<HostKey> KeysDown => NoKeys;
    public IReadOnlySet<GamepadButton> GamepadButtonsDown => NoButtons;
    public bool CapsLockOn => false;
    public void UpdatePerFrame() { }
    public string? DetectNativeKeyboardLayoutId() => null;
}
