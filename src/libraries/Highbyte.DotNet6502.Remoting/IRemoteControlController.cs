namespace Highbyte.DotNet6502.Remoting;

/// <summary>
/// Manages the lifecycle of a TCP remote control server.
/// Exposes connection state for UI binding.
/// </summary>
public interface IRemoteControlController
{
    bool IsListening { get; }
    bool IsClientConnected { get; }
    int Port { get; }
    event EventHandler? StateChanged;
    Task StartAsync(int port);
    Task StopAsync();
}
