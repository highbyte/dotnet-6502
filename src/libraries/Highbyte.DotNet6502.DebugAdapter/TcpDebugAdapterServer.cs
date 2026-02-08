using System.Net;
using System.Net.Sockets;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// TCP server that listens for debug adapter connections.
/// When a client connects, creates a TcpTransport and fires the ClientConnected event.
/// </summary>
public class TcpDebugAdapterServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private readonly StreamWriter _log;
    private bool _disposed;

    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;

    /// <summary>
    /// Gets the port the server is listening on.
    /// </summary>
    public int Port { get; private set; }

    public TcpDebugAdapterServer(StreamWriter log)
    {
        _log = log;
    }

    /// <summary>
    /// Starts the TCP server listening for connections.
    /// </summary>
    /// <param name="port">Port to listen on. Use 0 for a random available port.</param>
    public async Task StartAsync(int port = 0)
    {
        if (_listener != null)
            throw new InvalidOperationException("Server is already started");

        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();

        // Get the actual port (useful when port=0)
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _log.WriteLine($"[TCP Server] Started listening on port {Port}");
        _log.Flush();

        _cts = new CancellationTokenSource();
        _listenTask = ListenForClientsAsync(_cts.Token);
    }

    private async Task ListenForClientsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _log.WriteLine("[TCP Server] Waiting for client connection...");
                _log.Flush();

                var client = await _listener!.AcceptTcpClientAsync(cancellationToken);

                _log.WriteLine($"[TCP Server] Client connected from {client.Client.RemoteEndPoint}");
                _log.Flush();

                // Create transport and fire event
                var transport = new TcpTransport(client, _log);
                ClientConnected?.Invoke(this, new ClientConnectedEventArgs(transport));
            }
        }
        catch (OperationCanceledException)
        {
            _log.WriteLine("[TCP Server] Listen cancelled");
            _log.Flush();
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[TCP Server] Error: {ex.Message}");
            _log.Flush();
        }
    }

    /// <summary>
    /// Stops the TCP server.
    /// </summary>
    public void Stop()
    {
        _log.WriteLine("[TCP Server] Stopping...");
        _log.Flush();

        _cts?.Cancel();
        _listener?.Stop();

        if (_listenTask != null)
        {
            try
            {
                _listenTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch { }
        }

        _log.WriteLine("[TCP Server] Stopped");
        _log.Flush();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
        _cts?.Dispose();
        _listener?.Stop();
    }
}

/// <summary>
/// Event arguments for when a client connects to the debug adapter server.
/// </summary>
public class ClientConnectedEventArgs : EventArgs
{
    public IDebugAdapterTransport Transport { get; }

    public ClientConnectedEventArgs(IDebugAdapterTransport transport)
    {
        Transport = transport;
    }
}
