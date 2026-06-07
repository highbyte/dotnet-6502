using System.Collections.Concurrent;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Avalonia.Commodore64.Transport;

public sealed class WebSocketTransport : Systems.Commodore64.Transport.ISwiftLinkTransport
{
    private static readonly TimeSpan ReceiveLoopShutdownTimeout = TimeSpan.FromSeconds(1);

    private readonly Uri _bridgeUri;
    private readonly string? _sharedToken;
    private readonly ConcurrentQueue<byte> _receivedBytes = new();
    private readonly object _sync = new();
    private readonly ILogger _logger;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private bool _isConnected;
    private bool _warnedDisconnectedSend;
    private int _sentByteLogCount;
    private int _receivedByteLogCount;

    public WebSocketTransport(string bridgeUrl, string? sharedToken, string? targetId, ILogger logger)
    {
        _bridgeUri = BuildConnectionUri(bridgeUrl, sharedToken, targetId);
        _sharedToken = string.IsNullOrWhiteSpace(sharedToken) ? null : sharedToken.Trim();
        _logger = logger;
    }

    public bool IsConnected => _isConnected;
    public bool IsCarrierDetected => _isConnected;
    public bool IsDataSetReady => _isConnected;

    public static Uri BuildConnectionUri(string bridgeUrl, string? sharedToken, string? targetId)
    {
        if (!Uri.TryCreate(bridgeUrl?.Trim(), UriKind.Absolute, out var bridgeUri))
            throw new ArgumentException("Bridge URL must be an absolute URI.", nameof(bridgeUrl));

        if (bridgeUri.Scheme != Uri.UriSchemeWs && bridgeUri.Scheme != Uri.UriSchemeWss)
            throw new ArgumentException("Bridge URL must use ws:// or wss://.", nameof(bridgeUrl));

        if (string.IsNullOrWhiteSpace(sharedToken) && string.IsNullOrWhiteSpace(targetId))
            return bridgeUri;

        var builder = new UriBuilder(bridgeUri);
        var queryParameters = ParseQuery(builder.Query);
        if (!string.IsNullOrWhiteSpace(sharedToken))
            queryParameters["token"] = sharedToken.Trim();
        if (!string.IsNullOrWhiteSpace(targetId))
            queryParameters["target"] = targetId.Trim();
        builder.Query = string.Join("&", queryParameters.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return builder.Uri;
    }

    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        _logger.LogInformation("Connecting SwiftLink WebSocket transport to {BridgeUri}.", _bridgeUri);

        var webSocket = new ClientWebSocket();
        await webSocket.ConnectAsync(_bridgeUri, cancellationToken);
        var receiveCts = new CancellationTokenSource();

        lock (_sync)
        {
            _webSocket = webSocket;
            _receiveCts = receiveCts;
            _receiveTask = Task.Run(() => ReceiveLoopAsync(webSocket, receiveCts.Token), CancellationToken.None);
            _isConnected = true;
            _warnedDisconnectedSend = false;
            _sentByteLogCount = 0;
            _receivedByteLogCount = 0;
        }

        _logger.LogInformation("Connected SwiftLink WebSocket transport to {BridgeUri}.", _bridgeUri);
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        Task? receiveTask;
        CancellationTokenSource? receiveCts;
        ClientWebSocket? webSocket;

        lock (_sync)
        {
            receiveTask = _receiveTask;
            receiveCts = _receiveCts;
            webSocket = _webSocket;
            _receiveTask = null;
            _receiveCts = null;
            _webSocket = null;
            _isConnected = false;
        }

        receiveCts?.Cancel();

        if (PlatformDetection.IsRunningInWebAssembly())
        {
            if (webSocket != null)
            {
                try
                {
                    webSocket.Abort();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Ignoring SwiftLink WebSocket abort failure on WebAssembly.");
                }
            }

            receiveCts?.Dispose();
            Reset();
            _logger.LogInformation("Disconnected SwiftLink WebSocket transport from {BridgeUri}.", _bridgeUri);
            return;
        }

        if (webSocket != null)
        {
            try
            {
                if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "disconnect",
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }

            webSocket.Dispose();
        }

        if (receiveTask != null)
        {
            try
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    using var waitCts = new CancellationTokenSource(ReceiveLoopShutdownTimeout);
                    await receiveTask.WaitAsync(waitCts.Token);
                }
                else
                {
                    await receiveTask.WaitAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Timed out waiting for SwiftLink WebSocket transport reader loop to stop for {BridgeUri}.",
                    _bridgeUri);
            }
        }

        receiveCts?.Dispose();
        Reset();
        _logger.LogInformation("Disconnected SwiftLink WebSocket transport from {BridgeUri}.", _bridgeUri);
    }

    public bool TryDequeueReceivedByte(out byte value)
        => _receivedBytes.TryDequeue(out value);

    public async ValueTask SendAsync(byte value, CancellationToken cancellationToken = default)
    {
        var webSocket = _webSocket;
        if (webSocket == null || !_isConnected || webSocket.State != WebSocketState.Open)
        {
            if (!_warnedDisconnectedSend)
            {
                _logger.LogWarning("SwiftLink send attempted while WebSocket transport is not connected.");
                _warnedDisconnectedSend = true;
            }
            return;
        }

        await webSocket.SendAsync(
            new ArraySegment<byte>([value]),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cancellationToken);
        if (_sentByteLogCount < 32)
        {
            _sentByteLogCount++;
            _logger.LogInformation(
                "SwiftLink WebSocket transport sent byte 0x{Value:X2} to {BridgeUri}.",
                value,
                _bridgeUri);
        }
    }

    public void Reset()
    {
        while (_receivedBytes.TryDequeue(out _))
        {
        }
    }

    public void Dispose()
    {
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            DisposeAsyncOnBrowser();
            return;
        }

        DisconnectAsync().AsTask().GetAwaiter().GetResult();
    }

    private async void DisposeAsyncOnBrowser()
    {
        try
        {
            await DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ignoring SwiftLink WebSocket transport dispose failure on WebAssembly.");
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        while (!cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                lock (_sync)
                {
                    _isConnected = false;
                }
                break;
            }

            if (result.Count <= 0)
                continue;

            for (var i = 0; i < result.Count; i++)
            {
                _receivedBytes.Enqueue(buffer[i]);
                if (_receivedByteLogCount < 32)
                {
                    _receivedByteLogCount++;
                    _logger.LogInformation(
                        "SwiftLink WebSocket transport received byte 0x{Value:X2} from {BridgeUri}.",
                        buffer[i],
                        _bridgeUri);
                }
            }
        }

        lock (_sync)
        {
            _isConnected = false;
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmed))
            return parameters;

        foreach (var segment in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            parameters[key] = value;
        }

        return parameters;
    }
}
