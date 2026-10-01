namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// Serializes committed text into frame-scoped keyboard chords with release gaps.
/// Call after IInputInjector.BeginFrame, before input collection and CPU execution.
/// All access is on the host's emulation/UI thread.
/// </summary>
public sealed class TimedKeyboardInput
{
    private const int MaximumPendingCharacters = 256;
    private readonly Queue<char> _pending = new();
    private readonly int _pressFrames;
    private readonly int _releaseFrames;
    private IKeyboardTextInput? _target;
    private char _activeCharacter;
    private int _pressRemaining;
    private int _releaseRemaining;

    public TimedKeyboardInput(int pressFrames = 3, int releaseFrames = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pressFrames, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(releaseFrames, 1);
        _pressFrames = pressFrames;
        _releaseFrames = releaseFrames;
    }

    public bool TryEnqueue(IKeyboardTextInput target, string text)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(text);
        if (!ReferenceEquals(_target, target))
            Clear();
        text = text.Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r');
        // Reject a whole committed chunk rather than silently altering a command by
        // dropping unsupported characters, and bound bursts such as pasted text.
        if (_pending.Count + text.Length > MaximumPendingCharacters
            || text.Any(character => !target.CanPressCharacter(character)))
            return false;
        _target = target;
        foreach (var character in text)
            _pending.Enqueue(character);
        return true;
    }

    public void BeforeFrame(IKeyboardTextInput? target)
    {
        if (!ReferenceEquals(_target, target) || target == null)
        {
            Clear();
            return;
        }
        if (_releaseRemaining > 0)
        {
            _releaseRemaining--;
            return;
        }
        if (_pressRemaining == 0)
        {
            if (!_pending.TryDequeue(out _activeCharacter))
                return;
            _pressRemaining = _pressFrames;
        }
        target.PressCharacter(_activeCharacter);
        if (--_pressRemaining == 0)
            _releaseRemaining = _releaseFrames;
    }

    public void Clear()
    {
        _pending.Clear();
        _target = null;
        _pressRemaining = 0;
        _releaseRemaining = 0;
    }
}
