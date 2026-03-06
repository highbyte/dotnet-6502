using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Scripting;

/// <summary>
/// Null-object implementation of <see cref="IScriptingEngine"/>.
/// Used when scripting is disabled or no scripting backend is available (e.g. WASM).
/// All methods are no-ops.
/// </summary>
public class NoScriptingEngine : IScriptingEngine
{
    public bool IsEnabled => false;
    public void LoadScripts() { }
    public void OnSystemStarted(ISystem system) { }
    public void InvokeBeforeFrame() { }
    public void ResumeCoroutines() { }
    public void InvokeAfterFrame() { }
    public void InvokeEvent(string hookName, params object[] args) { }
    public void SetEmulatorControl(IEmulatorControl? control) { }
}
