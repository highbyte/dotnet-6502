namespace Highbyte.DotNet6502.Remoting;

/// <summary>
/// Manages the lifecycle of a TCP remote control server.
/// Exposes connection state for UI binding.
/// </summary>
public interface IRemoteControlController
{
    /// <summary>
    /// Default TCP port for the remote control server.
    /// </summary>
    const int DefaultPort = 6510;

    /// <summary>
    /// Default bind address for the remote control server.
    /// Loopback-only so that remote network access is opt-in.
    /// </summary>
    const string DefaultBindAddress = "127.0.0.1";

    bool IsListening { get; }
    bool IsClientConnected { get; }
    int Port { get; }
    string BindAddress { get; }
    event EventHandler? StateChanged;

    /// <summary>
    /// Starts the TCP remote control server.
    /// </summary>
    /// <param name="port">TCP port to listen on (1-65535).</param>
    /// <param name="bindAddress">
    /// IP address to bind to. If <c>null</c> or empty the server binds to loopback (<c>127.0.0.1</c>).
    /// Use <c>0.0.0.0</c> to accept connections from any network interface.
    /// </param>
    Task StartAsync(int port, string? bindAddress = null);
    Task StopAsync();
}
