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

    /// <summary>
    /// Gets whether the server is currently listening for connections.
    /// </summary>
    public bool IsListening { get; private set; }

    public TcpDebugAdapterServer(StreamWriter log)
    {
        _log = log;
    }

    /// <summary>
    /// Starts the TCP server listening for connections.
    /// Can be called again after <see cref="Stop"/> to restart the server.
    /// </summary>
    /// <param name="port">Port to listen on. Use 0 for a random available port.</param>
    public async Task StartAsync(int port = 0)
    {
        if (IsListening)
            throw new InvalidOperationException("Server is already started");

        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();

        // Get the actual port (useful when port=0)
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _log.WriteLine($"[TCP Server] Started listening on port {Port}");
        _log.Flush();

        _cts = new CancellationTokenSource();
        // Run on the thread pool to avoid capturing the UI synchronization context.
        // Without Task.Run, if StartAsync is called from the UI thread, the async
        // continuations inside ListenForClientsAsync would be scheduled back onto the
        // UI thread. Then calling _listenTask.Wait() on the UI thread in Stop() would
        // deadlock: Wait() blocks the UI thread, while the continuation needs the UI
        // thread to run. The 5-second timeout would expire, Dispose() would close the
        // StreamWriter, and the finally-running continuation would throw
        // ObjectDisposedException.
        _listenTask = Task.Run(() => ListenForClientsAsync(_cts.Token));
        IsListening = true;
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
            SafeLog("[TCP Server] Listen cancelled");
        }
        catch (Exception ex)
        {
            SafeLog($"[TCP Server] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes to the log, silently swallowing ObjectDisposedException.
    /// Used in long-running background tasks where the writer may be closed
    /// concurrently if Stop() times out waiting for the task.
    /// </summary>
    private void SafeLog(string message)
    {
        try
        {
            _log.WriteLine(message);
            _log.Flush();
        }
        catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// Stops the TCP server. The server can be restarted by calling <see cref="StartAsync"/> again.
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

        // Reset state to allow restart via StartAsync
        _listener = null;
        _cts = null;
        _listenTask = null;
        IsListening = false;

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
