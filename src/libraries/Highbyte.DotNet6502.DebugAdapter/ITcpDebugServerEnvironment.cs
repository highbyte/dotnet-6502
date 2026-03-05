namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Abstracts the host-environment operations needed by <see cref="TcpDebugServerManager"/>,
/// allowing it to run without a direct dependency on any specific UI framework.
/// </summary>
public interface ITcpDebugServerEnvironment
{
    /// <summary>
    /// Returns the current debuggable host app, or null if the host has not yet
    /// initialised (e.g. the TCP server started before the UI framework is ready).
    /// </summary>
    IDebuggableHostApp? GetHostApp();

    /// <summary>
    /// Schedules <paramref name="action"/> to run on the UI thread.
    /// Required because host app operations (SetExternalDebugAdapter, ClearExternalDebugAdapter)
    /// must execute on the thread that owns the emulator run loop.
    /// </summary>
    void RunOnUiThread(Action action);

    /// <summary>
    /// Terminates the host application.
    /// Called when a launch-mode debug session ends with terminateDebuggee = true.
    /// </summary>
    void TerminateApplication();
}
