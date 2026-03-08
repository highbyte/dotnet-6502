namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Provides Lua scripts with the ability to control basic emulator operations.
/// Implementations must be thread-safe: control request methods may be called from the emulator loop thread.
/// Requests are typically deferred and executed after the current frame completes.
/// </summary>
public interface IEmulatorControl
{
    /// <summary>Names of all registered emulator systems.</summary>
    IReadOnlyList<string> AvailableSystems { get; }

    /// <summary>Name of the currently selected system, e.g. "C64".</summary>
    string SelectedSystem { get; }

    /// <summary>Name of the currently selected system configuration variant.</summary>
    string SelectedVariant { get; }

    /// <summary>
    /// Current emulator state as a lowercase string: "running", "paused", or "stopped".
    /// </summary>
    string CurrentState { get; }

    /// <summary>Deferred: start the emulator. No-op if already running.</summary>
    void RequestStart();

    /// <summary>Deferred: pause the emulator. No-op if already paused or stopped.</summary>
    void RequestPause();

    /// <summary>Deferred: stop the emulator. No-op if already stopped.</summary>
    void RequestStop();

    /// <summary>Deferred: stop then restart the emulator.</summary>
    void RequestReset();

    /// <summary>
    /// Deferred: select a system (and optionally a configuration variant).
    /// The emulator must be stopped before this takes effect.
    /// </summary>
    void RequestSelectSystem(string systemName, string? variant = null);
}
