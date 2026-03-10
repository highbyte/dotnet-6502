namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Snapshot of a loaded script's current status, returned by <see cref="IScriptingEngine.GetScriptStatuses"/>.
/// </summary>
public record ScriptStatus(
    /// <summary>File name of the script (e.g. "example_monitor.lua").</summary>
    string FileName,
    /// <summary>High-level execution state of the script.</summary>
    ScriptExecutionState State,
    /// <summary>How the script's coroutine last yielded (null if not a coroutine-based script).</summary>
    ScriptYieldType? YieldType,
    /// <summary>Hook function names that this script defines (e.g. "on_before_frame", "on_after_frame").</summary>
    IReadOnlyList<string> Hooks,
    /// <summary>Whether the user can toggle this script's enabled state.</summary>
    bool CanToggle,
    /// <summary>Whether this script can be reloaded from disk (not currently running).</summary>
    bool CanReload
);

/// <summary>
/// High-level execution state of a script.
/// </summary>
public enum ScriptExecutionState
{
    /// <summary>Script has an active coroutine that is currently suspended (will be resumed).</summary>
    Running,
    /// <summary>Script's coroutine was disabled due to exceeding the instruction limit.</summary>
    Disabled,
    /// <summary>Script was explicitly disabled by the user at runtime. Can be re-enabled.</summary>
    UserDisabled,
    /// <summary>Script's coroutine has completed (Dead state). It may still have hook functions active.</summary>
    Completed,
    /// <summary>Script only defines hook functions, it has no coroutine loop.</summary>
    HookOnly
}

/// <summary>
/// The yield primitive a coroutine last used.
/// </summary>
public enum ScriptYieldType
{
    /// <summary>Yielded via <c>emu.frameadvance()</c> — resumed each emulator frame.</summary>
    FrameAdvance,
    /// <summary>Yielded via <c>emu.yield()</c> — resumed each timer tick, even when paused.</summary>
    Tick,
    /// <summary>Yielded because an async HTTP call is in-flight. Resumed when the task completes.</summary>
    HttpPending
}
