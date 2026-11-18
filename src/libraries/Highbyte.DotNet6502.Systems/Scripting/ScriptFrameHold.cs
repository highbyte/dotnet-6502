namespace Highbyte.DotNet6502.Systems;

/// <summary>A single Lua invocation owns the boundary until it returns or is cancelled.</summary>
public sealed class ScriptFrameHold(string hookName, string scriptFile)
{
    public string HookName { get; } = hookName;
    public string ScriptFile { get; } = scriptFile;
    public bool Finished { get; private set; }
    public bool Failed { get; private set; }
    public void Complete() => Finished = true;
    public void Fail() => Failed = true;

    // A held after-hook targets the following frame, whose BeginFrame clears transient input.
    // Stage its writes until then. The same staging discards a failed before-hook's input.
    private readonly List<Action<IInputInjector>> _input = [];
    public void KeyPress(string key) => _input.Add(input => input.KeyPress(key));
    public void KeyRelease(string key) => _input.Add(input => input.KeyRelease(key));
    public void KeyReleaseAll() => _input.Add(input => input.KeyReleaseAll());
    public void SetJoystickAction(int port, string action, bool pressed)
        => _input.Add(input => input.SetJoystickAction(port, action, pressed));

    internal void ApplyInput(IInputInjector? injector)
    {
        if (injector == null) return;
        foreach (var write in _input) write(injector);
    }
}
