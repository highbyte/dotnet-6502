using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Scripting;

/// <summary>
/// Lua-engine-specific operations required by <see cref="ScriptingEngine"/>.
/// Implement this interface to support a different Lua backend (MoonSharp, NLua, etc.).
/// All engine-agnostic orchestration (script enable/disable, status tracking, event firing)
/// lives in <see cref="ScriptingEngine"/>; only the VM-level operations live here.
/// </summary>
public interface IScriptingEngineAdapter
{
    // --- Lifecycle ---

    /// <summary>
    /// Initialize the Lua VM, register global tables (cpu, mem, log, emu),
    /// and attach the emulator control so scripts can call emu.start() etc.
    /// Called once at the start of <see cref="ScriptingEngine.LoadScripts"/>.
    /// <paramref name="getFrameCount"/> and <paramref name="getElapsedSeconds"/> are used by the
    /// emu.framecount() and emu.time() Lua callbacks; they read values owned by <see cref="ScriptingEngine"/>.
    /// </summary>
    void InitializeVm(
        IEmulatorControl? emulatorControl,
        ScriptingConfig config,
        ILogger logger,
        Func<int> getFrameCount,
        Func<double> getElapsedSeconds);

    /// <summary>
    /// Update the cpu and mem globals to reflect the currently running system.
    /// Called on every system start (including after reset).
    /// </summary>
    void OnSystemStarted(ISystem system);

    // --- Script loading ---

    /// <summary>
    /// Compile a single .lua file without executing it.
    /// Returns an opaque handle on success, or null on compile failure (syntax error etc.).
    /// </summary>
    AdapterScriptHandle? LoadFile(string filePath, string fileName);

    /// <summary>
    /// Performs the initial (first) resume of a script's coroutine:
    /// runs top-level code until the first yield or completion.
    /// Returns the resulting state, including which hook functions the script registered.
    /// </summary>
    AdapterScriptState InitialResume(AdapterScriptHandle handle);

    // --- Frame lifecycle ---

    /// <summary>
    /// Resume all coroutines that last yielded via emu.frameadvance().
    /// <paramref name="activeHandles"/> contains only non-disabled, non-failed handles —
    /// filtering by user/auto-disabled state is done by <see cref="ScriptingEngine"/> before calling.
    /// Results are reported via <paramref name="onResult"/> to avoid per-frame list allocations.
    /// </summary>
    void ResumeFrameAdvanceCoroutines(
        IReadOnlyList<AdapterScriptHandle> activeHandles,
        Action<AdapterScriptHandle, AdapterResumeResult> onResult);

    /// <summary>
    /// Resume all coroutines that last yielded via emu.yield().
    /// Same contract as <see cref="ResumeFrameAdvanceCoroutines"/>.
    /// </summary>
    void ResumeTickCoroutines(
        IReadOnlyList<AdapterScriptHandle> activeHandles,
        Action<AdapterScriptHandle, AdapterResumeResult> onResult);

    /// <summary>
    /// Invoke a zero-argument hook function (e.g. on_before_frame, on_after_frame).
    /// Hot path — called at ~60 Hz. Returns false if the hook threw a runtime error.
    /// </summary>
    bool InvokeHook(string hookName, string scriptFile);

    /// <summary>
    /// Invoke a hook function with arguments (e.g. on_started, on_system_selected).
    /// Not a hot path. Returns false if the hook threw a runtime error.
    /// </summary>
    bool InvokeHookWithArgs(string hookName, string scriptFile, params object[] args);

    // --- Status queries ---

    /// <summary>
    /// Query the current coroutine state for a specific handle.
    /// Used by <see cref="ScriptingEngine.GetScriptStatuses"/> to build the status snapshot.
    /// </summary>
    AdapterScriptState GetScriptState(AdapterScriptHandle handle);

    // --- Hot-reload ---

    /// <summary>
    /// Recompile a script file and replace the coroutine in the handle in-place.
    /// The handle's position in <see cref="ScriptingEngine"/>'s list is preserved.
    /// Returns true on success, false on compile failure.
    /// </summary>
    bool RecompileFile(AdapterScriptHandle handle, string filePath);

    /// <summary>
    /// Rebuild any internal hook function reference cache after scripts are loaded or reloaded.
    /// Called by <see cref="ScriptingEngine"/> after all initial resumes complete.
    /// </summary>
    void RebuildHookCache();
}
