using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Scripting;

/// <summary>
/// Abstraction for a scripting engine that can run scripts hooked into the emulator's frame cycle.
/// Implement this interface to support different scripting backends (e.g. MoonSharp, NLua).
/// </summary>
public interface IScriptingEngine
{
    /// <summary>
    /// Whether this scripting engine is active. Returns false for the null-object implementation.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Called once when the emulator system starts (or restarts after reset).
    /// Should load all scripts from the configured directory and register the emulator API.
    /// </summary>
    /// <param name="system">The running emulator system (provides access to CPU and memory).</param>
    void Initialize(ISystem system);

    /// <summary>
    /// Called by the host app before each emulator frame executes.
    /// Scripts may define an <c>on_before_frame()</c> function that will be invoked here.
    /// </summary>
    void InvokeBeforeFrame();

    /// <summary>
    /// Called by the host app after each emulator frame completes.
    /// Scripts may define an <c>on_after_frame()</c> function that will be invoked here.
    /// </summary>
    void InvokeAfterFrame();
}
