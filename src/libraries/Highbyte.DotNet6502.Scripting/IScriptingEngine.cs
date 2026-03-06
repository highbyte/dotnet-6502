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
    /// Called once at application startup (before any system starts) to load and initialize scripts.
    /// Scripts may call emulator control operations (<c>emu.start()</c> etc.) from their top-level code.
    /// CPU and memory globals exist but return safe defaults until <see cref="OnSystemStarted"/> is called.
    /// </summary>
    void LoadScripts();

    /// <summary>
    /// Called each time the emulator system starts (including after a reset).
    /// Updates the <c>cpu</c> and <c>mem</c> script globals to reflect the running system.
    /// </summary>
    /// <param name="system">The running emulator system (provides access to CPU and memory).</param>
    void OnSystemStarted(ISystem system);

    /// <summary>
    /// Called by the host app before each emulator frame executes.
    /// Scripts may define an <c>on_before_frame()</c> function that will be invoked here.
    /// </summary>
    void InvokeBeforeFrame();

    /// <summary>
    /// Resumes only coroutines that last yielded via <c>emu.yield()</c>.
    /// These coroutines tick on every timer interval regardless of emulator state,
    /// allowing them to observe state and request control operations (e.g. <c>emu.start()</c>)
    /// while the emulator is paused.
    /// Coroutines that yielded via <c>emu.frameadvance()</c> are not resumed.
    /// </summary>
    void ResumeCoroutines();

    /// <summary>
    /// Called by the host app after each emulator frame completes.
    /// Scripts may define an <c>on_after_frame()</c> function that will be invoked here.
    /// </summary>
    void InvokeAfterFrame();

    /// <summary>
    /// Invokes a named event hook function if defined by a loaded script.
    /// <para>
    /// Standard event hooks:
    /// <list type="bullet">
    ///   <item><c>on_started()</c> — emulator started or resumed</item>
    ///   <item><c>on_paused()</c> — emulator paused</item>
    ///   <item><c>on_stopped()</c> — emulator stopped</item>
    ///   <item><c>on_system_selected(name)</c> — system selection changed</item>
    ///   <item><c>on_variant_selected(name)</c> — system variant changed</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="hookName">The Lua function name to invoke (e.g. <c>"on_started"</c>).</param>
    /// <param name="args">Optional arguments passed to the Lua function.</param>
    void InvokeEvent(string hookName, params object[] args);

    /// <summary>
    /// Provides the scripting engine with an <see cref="IEmulatorControl"/> so that scripts can
    /// request emulator operations (start, stop, pause, reset, select system).
    /// Call this before <see cref="LoadScripts"/>. Pass <c>null</c> to disconnect.
    /// </summary>
    void SetEmulatorControl(IEmulatorControl? control);
}
