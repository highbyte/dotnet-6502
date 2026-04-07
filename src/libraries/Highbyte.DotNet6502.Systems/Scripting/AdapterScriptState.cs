namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// The coroutine state reported by an <see cref="IScriptingEngineAdapter"/> for a single script handle.
/// </summary>
public enum AdapterCoroutineState
{
    /// <summary>Coroutine yielded normally and can be resumed.</summary>
    Suspended,
    /// <summary>Coroutine finished (returned from top-level code).</summary>
    Dead,
    /// <summary>Coroutine exceeded the instruction limit and cannot be resumed. Unrecoverable.</summary>
    ForceSuspended,
    /// <summary>Coroutine terminated due to a Lua runtime error.</summary>
    RuntimeError,
}

/// <summary>
/// State snapshot for a single script, returned by <see cref="IScriptingEngineAdapter.InitialResume"/>
/// and <see cref="IScriptingEngineAdapter.GetScriptState"/>.
/// </summary>
public record AdapterScriptState(
    AdapterCoroutineState CoroutineState,
    ScriptYieldType? LastYieldType,
    IReadOnlyList<string> RegisteredHooks
);
