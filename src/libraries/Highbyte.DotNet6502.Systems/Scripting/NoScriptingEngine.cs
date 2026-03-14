namespace Highbyte.DotNet6502.Systems;

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
    public void SetHostApp(IHostApp? hostApp) { }
    public Task DrainPendingActionsAsync() => Task.CompletedTask;
    public IReadOnlyList<ScriptStatus> GetScriptStatuses() => Array.Empty<ScriptStatus>();
    public event EventHandler? ScriptStatusChanged { add { } remove { } }
    public void SetScriptEnabled(string fileName, bool enabled) { }
    public void ReloadScript(string fileName) { }
    public void ReloadAllScripts() { }
    public string ScriptDirectory => string.Empty;
    public bool CanManageScripts => false;
    public void UpsertScript(string fileName, string content) { }
    public void DeleteScript(string fileName) { }
}
