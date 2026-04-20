using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Remoting.Tcp;

/// <summary>
/// TCP server that listens for remote control connections.
/// Accepts one client at a time and fires <see cref="ClientConnected"/> for each.
/// Mirrors the structure of <c>TcpDebugAdapterServer</c>.
/// </summary>
public class TcpRemoteControlServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private readonly ILogger<TcpRemoteControlServer>? _logger;
    private bool _disposed;

    public event EventHandler<TcpClientConnectedEventArgs>? ClientConnected;

    public int Port { get; private set; }
    public bool IsListening { get; private set; }

    public TcpRemoteControlServer(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<TcpRemoteControlServer>();
    }

    public async Task StartAsync(int port = 0)
    {
        if (IsListening)
            throw new InvalidOperationException("Server is already started");

        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _logger?.LogInformation("[RemoteControl TCP] Started listening on port {Port}", Port);

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenForClientsAsync(_cts.Token));
        IsListening = true;

        await Task.CompletedTask;
    }

    private async Task ListenForClientsAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                _logger?.LogDebug("[RemoteControl TCP] Waiting for client connection...");
                var client = await _listener!.AcceptTcpClientAsync(ct);
                _logger?.LogInformation("[RemoteControl TCP] Client connected from {Endpoint}", client.Client.RemoteEndPoint);
                ClientConnected?.Invoke(this, new TcpClientConnectedEventArgs(client));
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("[RemoteControl TCP] Listen cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("[RemoteControl TCP] Error: {Message}", ex.Message);
        }
    }

    public void Stop()
    {
        _logger?.LogInformation("[RemoteControl TCP] Stopping...");
        _cts?.Cancel();
        _listener?.Stop();

        if (_listenTask != null)
        {
            try { _listenTask.Wait(TimeSpan.FromSeconds(5)); } catch { }
        }

        _listener = null;
        _cts = null;
        _listenTask = null;
        IsListening = false;

        _logger?.LogInformation("[RemoteControl TCP] Stopped");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts?.Dispose();
    }
}

public class TcpClientConnectedEventArgs : EventArgs
{
    public TcpClient Client { get; }
    public TcpClientConnectedEventArgs(TcpClient client) => Client = client;
}
