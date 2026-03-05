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
    public void Initialize(ISystem system) { }
    public void InvokeBeforeFrame() { }
    public void InvokeAfterFrame() { }
}
