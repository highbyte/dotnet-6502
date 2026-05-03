using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<TcpDebugAdapterServer>? _logger;
    private bool _disposed;

    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;

    /// <summary>
    /// Gets the port the server is listening on.
    /// </summary>
    public int Port { get; private set; }

    /// <summary>
    /// Gets the IP address the server is bound to.
    /// </summary>
    public IPAddress BindAddress { get; private set; } = IPAddress.Loopback;

    /// <summary>
    /// Gets whether the server is currently listening for connections.
    /// </summary>
    public bool IsListening { get; private set; }

    public TcpDebugAdapterServer(StreamWriter log, ILoggerFactory? loggerFactory = null)
    {
        _log = log;
        _logger = loggerFactory?.CreateLogger<TcpDebugAdapterServer>();
    }

    /// <summary>
    /// Starts the TCP server listening for connections.
    /// Can be called again after <see cref="Stop"/> to restart the server.
    /// </summary>
    /// <param name="port">Port to listen on. Use 0 for a random available port.</param>
    /// <param name="bindAddress">IP address to bind to. Defaults to loopback.</param>
    public async Task StartAsync(int port = 0, IPAddress? bindAddress = null)
    {
        if (IsListening)
            throw new InvalidOperationException("Server is already started");

        BindAddress = bindAddress ?? IPAddress.Loopback;
        _listener = new TcpListener(BindAddress, port);
        _listener.Start();

        // Get the actual port (useful when port=0)
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        SafeLog($"[TCP Server] Started listening on {BindAddress}:{Port}", LogLevel.Information);

        _cts = new CancellationTokenSource();
        // Run on the thread pool to avoid capturing the caller's synchronization context.
        // This keeps the accept loop independent from any UI thread and allows Stop()
        // to cancel the listener without blocking the caller while shutdown completes.
        _listenTask = Task.Run(() => ListenForClientsAsync(_cts.Token));
        IsListening = true;
    }

    private async Task ListenForClientsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SafeLog("[TCP Server] Waiting for client connection...");

                var client = await _listener!.AcceptTcpClientAsync(cancellationToken);

                SafeLog($"[TCP Server] Client connected from {client.Client.RemoteEndPoint}", LogLevel.Information);

                // Create transport and fire event
                var transport = new TcpTransport(client, _log);
                ClientConnected?.Invoke(this, new ClientConnectedEventArgs(transport));
            }
        }
        catch (OperationCanceledException)
        {
            SafeLog("[TCP Server] Listen cancelled", LogLevel.Information);
        }
        catch (Exception ex)
        {
            SafeLog($"[TCP Server] Error: {ex.Message}", LogLevel.Warning);
        }
    }

    /// <summary>
    /// Writes to the log file and ILogger, silently swallowing ObjectDisposedException.
    /// Used throughout to ensure log writes never throw even if the StreamWriter is
    /// closed concurrently (e.g. if Stop() times out waiting for a background task).
    /// </summary>
    private void SafeLog(string message, LogLevel level = LogLevel.Debug)
    {
        _logger?.Log(level, "{Message}", message);
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
        SafeLog("[TCP Server] Stopping...", LogLevel.Information);

        var cts = _cts;
        var listener = _listener;
        var listenTask = _listenTask;

        cts?.Cancel();
        listener?.Stop();

        if (listenTask != null)
        {
            ObserveStopTask(listenTask);
        }

        // Reset state to allow restart via StartAsync
        _listener = null;
        _cts = null;
        _listenTask = null;
        IsListening = false;

        cts?.Dispose();

        SafeLog("[TCP Server] Stopped", LogLevel.Information);
    }

    private void ObserveStopTask(Task task)
    {
        if (task.IsCompleted)
        {
            LogStopFailure(task);
            return;
        }

        _ = task.ContinueWith(
            static (completedTask, state) => ((TcpDebugAdapterServer)state!).LogStopFailure(completedTask),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void LogStopFailure(Task task)
    {
        if (task.Exception is { } exception)
        {
            SafeLog($"[TCP Server] Listen task ended with error: {exception.GetBaseException().Message}", LogLevel.Warning);
        }
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
