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

    bool IsListening { get; }
    bool IsClientConnected { get; }
    int Port { get; }
    event EventHandler? StateChanged;
    Task StartAsync(int port);
    Task StopAsync();
}
