using System.Net;
using Highbyte.DotNet6502.Remoting.Tcp;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Remoting;

/// <summary>
/// Owns the <see cref="TcpRemoteControlServer"/> lifecycle and manages one active session at a time.
/// Exposes <see cref="IsClientConnected"/> and <see cref="Port"/> for UI binding via <see cref="StateChanged"/>.
/// </summary>
public class RemoteControlController : IRemoteControlController, IDisposable
{
    private readonly IRemoteControlEnvironment _environment;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<RemoteControlController>? _logger;
    private TcpRemoteControlServer? _server;
    private CancellationTokenSource? _sessionCts;
    private bool _disposed;

    private bool _isListening;
    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (_isListening == value) return;
            _isListening = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool _isClientConnected;
    public bool IsClientConnected
    {
        get => _isClientConnected;
        private set
        {
            if (_isClientConnected == value) return;
            _isClientConnected = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int Port { get; private set; }
    public string BindAddress { get; private set; } = IRemoteControlController.DefaultBindAddress;

    public event EventHandler? StateChanged;

    public RemoteControlController(IRemoteControlEnvironment environment, ILoggerFactory? loggerFactory = null)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<RemoteControlController>();
    }

    public async Task StartAsync(int port, string? bindAddress = null)
    {
        if (_isListening) return;

        var effectiveAddress = string.IsNullOrWhiteSpace(bindAddress)
            ? IRemoteControlController.DefaultBindAddress
            : bindAddress.Trim();

        if (!IPAddress.TryParse(effectiveAddress, out var parsedAddress))
            throw new ArgumentException(
                $"Invalid bind address '{effectiveAddress}'. Expected an IPv4 or IPv6 literal (e.g. 127.0.0.1, 0.0.0.0, ::1).",
                nameof(bindAddress));

        Port = port;
        BindAddress = effectiveAddress;
        _server = new TcpRemoteControlServer(_loggerFactory);
        _server.ClientConnected += OnClientConnected;
        await _server.StartAsync(port, parsedAddress);
        Port = _server.Port;
        IsListening = true;

        _logger?.LogInformation("[RemoteControl] Listening on {BindAddress}:{Port}", BindAddress, Port);
    }

    public async Task StopAsync()
    {
        if (_server == null || !_isListening) return;

        _sessionCts?.Cancel();
        _server.ClientConnected -= OnClientConnected;
        _server.Stop();
        _server.Dispose();
        _server = null;

        IsListening = false;
        IsClientConnected = false;

        _logger?.LogInformation("[RemoteControl] Stopped");
        await Task.CompletedTask;
    }

    private void OnClientConnected(object? sender, TcpClientConnectedEventArgs e)
    {
        // Cancel any existing session first
        _sessionCts?.Cancel();
        _sessionCts = new CancellationTokenSource();

        IsClientConnected = true;

        var dispatcher = new RemoteCommandDispatcher(_environment, _loggerFactory);
        var session = new RemoteControlSession(e.Client, dispatcher, _loggerFactory);
        var token = _sessionCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await session.RunAsync(token);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("[RemoteControl] Session error: {Message}", ex.Message);
            }
            finally
            {
                IsClientConnected = false;
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _server?.Dispose();
    }
}
