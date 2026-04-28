namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Controls the external TCP debug adapter server lifecycle at runtime.
/// Implemented by Desktop; the interface lives in the shared DebugAdapter library
/// so the Core (Avalonia) layer can reference it without a platform dependency.
/// Browser targets register no implementation — <see cref="App.ExternalDebugController"/>
/// will be null and the UI section will be hidden.
/// </summary>
public interface IExternalDebugController
{
    /// <summary>
    /// Default bind address for the debug adapter server.
    /// Loopback-only so external debugging is opt-in.
    /// </summary>
    const string DefaultBindAddress = "127.0.0.1";

    /// <summary>Whether the TCP server is currently listening for connections.</summary>
    bool IsListening { get; }

    /// <summary>Whether a DAP client is currently connected.</summary>
    bool IsClientConnected { get; }

    /// <summary>The port the server is (or was last) listening on.</summary>
    int Port { get; }

    /// <summary>The IP address the server is (or was last) bound to.</summary>
    string BindAddress { get; }

    /// <summary>
    /// Starts listening for incoming DAP connections on <paramref name="bindAddress"/>:<paramref name="port"/>.
    /// No-op if already listening. If <paramref name="bindAddress"/> is null or empty, loopback is used.
    /// </summary>
    Task StartAsync(int port, string? bindAddress = null);

    /// <summary>
    /// Stops accepting new connections. Any active session continues until the client
    /// disconnects. Can be called only when <see cref="IsClientConnected"/> is false.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Raised (possibly from a background thread) when <see cref="IsListening"/> or
    /// <see cref="IsClientConnected"/> changes.
    /// </summary>
    event EventHandler StateChanged;
}
