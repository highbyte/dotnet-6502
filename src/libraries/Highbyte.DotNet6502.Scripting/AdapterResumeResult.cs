using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Scripting;

/// <summary>
/// The result of resuming a single script coroutine, reported back to <see cref="ScriptingEngine"/>
/// via the callback passed to <see cref="IScriptingEngineAdapter.ResumeFrameAdvanceCoroutines"/>
/// or <see cref="IScriptingEngineAdapter.ResumeTickCoroutines"/>.
/// </summary>
public record AdapterResumeResult(
    AdapterCoroutineState NewState,
    ScriptYieldType? YieldType
);
