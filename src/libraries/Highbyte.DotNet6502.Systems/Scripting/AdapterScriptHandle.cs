namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Opaque token representing a single loaded script inside an <see cref="IScriptingEngineAdapter"/>.
/// <see cref="ScriptingEngine"/> holds these but does not interpret their internals.
/// Each Lua engine implementation subclasses this with its engine-specific coroutine state.
/// </summary>
public abstract class AdapterScriptHandle
{
    public string FileName { get; }

    protected AdapterScriptHandle(string fileName) => FileName = fileName;
}
