using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Highbyte.DotNet6502.Systems.Commodore64.Transport;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Browser.WebSocket;

[SupportedOSPlatform("browser")]
public sealed partial class BrowserWebSocketTransport : ISwiftLinkTransport
{
    private readonly Uri _bridgeUri;
    private readonly ConcurrentQueue<byte> _receivedBytes = new();
    private readonly ILogger _logger;
    private readonly int _connectionId;
    private bool _isConnected;
    private bool _warnedDisconnectedSend;
    private int _sentByteLogCount;
    private int _receivedByteLogCount;
    private static int s_nextConnectionId;

    public BrowserWebSocketTransport(string bridgeUrl, string? sharedToken, ILogger logger)
    {
        _bridgeUri = BuildConnectionUri(bridgeUrl, sharedToken);
        _logger = logger;
        _connectionId = Interlocked.Increment(ref s_nextConnectionId);
    }

    public bool IsConnected => _isConnected;
    public bool IsCarrierDetected => _isConnected;
    public bool IsDataSetReady => _isConnected;

    public static async Task LoadJsModuleAsync()
    {
        var jsModuleUri = BrowserWebSocketBridgeResources.GetJavaScriptModuleDataUri();
        await JSHost.ImportAsync("BrowserWebSocketBridge", jsModuleUri);
    }

    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        _logger.LogInformation("Connecting SwiftLink browser WebSocket transport to {BridgeUri}.", _bridgeUri);
        await JSInterop.OpenAsync(_connectionId, _bridgeUri.ToString());
        _isConnected = JSInterop.GetReadyState(_connectionId) == 1;
        _warnedDisconnectedSend = false;
        _sentByteLogCount = 0;
        _receivedByteLogCount = 0;
        _logger.LogInformation("Connected SwiftLink browser WebSocket transport to {BridgeUri}.", _bridgeUri);
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            JSInterop.Close(_connectionId, 1000, "disconnect");
        }
        catch
        {
        }

        _isConnected = false;
        Reset();
        JSInterop.Cleanup(_connectionId);
        _logger.LogInformation("Disconnected SwiftLink browser WebSocket transport from {BridgeUri}.", _bridgeUri);
        return ValueTask.CompletedTask;
    }

    public bool TryDequeueReceivedByte(out byte value)
    {
        RefreshReceivedQueue();
        return _receivedBytes.TryDequeue(out value);
    }

    public ValueTask SendAsync(byte value, CancellationToken cancellationToken = default)
    {
        RefreshConnectionState();
        if (!_isConnected)
        {
            if (!_warnedDisconnectedSend)
            {
                _logger.LogWarning("SwiftLink send attempted while browser WebSocket transport is not connected.");
                _warnedDisconnectedSend = true;
            }

            return ValueTask.CompletedTask;
        }

        JSInterop.SendByte(_connectionId, value);
        if (_sentByteLogCount < 32)
        {
            _sentByteLogCount++;
            _logger.LogInformation(
                "SwiftLink browser WebSocket transport sent byte 0x{Value:X2} to {BridgeUri}.",
                value,
                _bridgeUri);
        }

        return ValueTask.CompletedTask;
    }

    public void Reset()
    {
        while (_receivedBytes.TryDequeue(out _))
        {
        }
    }

    public void Dispose()
    {
        DisconnectAsync().AsTask().GetAwaiter().GetResult();
    }

    private void RefreshReceivedQueue()
    {
        RefreshConnectionState();

        var drained = JSInterop.DrainReceived(_connectionId);
        if (string.IsNullOrWhiteSpace(drained))
            return;

        foreach (var part in drained.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!byte.TryParse(part, out var value))
                continue;

            _receivedBytes.Enqueue(value);
            if (_receivedByteLogCount < 32)
            {
                _receivedByteLogCount++;
                _logger.LogInformation(
                    "SwiftLink browser WebSocket transport received byte 0x{Value:X2} from {BridgeUri}.",
                    value,
                    _bridgeUri);
            }
        }
    }

    private void RefreshConnectionState()
    {
        _isConnected = JSInterop.GetReadyState(_connectionId) == 1;
    }

    private static Uri BuildConnectionUri(string bridgeUrl, string? sharedToken)
    {
        if (!Uri.TryCreate(bridgeUrl?.Trim(), UriKind.Absolute, out var bridgeUri))
            throw new ArgumentException("Bridge URL must be an absolute URI.", nameof(bridgeUrl));

        if (bridgeUri.Scheme != Uri.UriSchemeWs && bridgeUri.Scheme != Uri.UriSchemeWss)
            throw new ArgumentException("Bridge URL must use ws:// or wss://.", nameof(bridgeUrl));

        if (string.IsNullOrWhiteSpace(sharedToken))
            return bridgeUri;

        var builder = new UriBuilder(bridgeUri);
        var query = builder.Query.TrimStart('?');
        var queryParameters = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(query))
        {
            foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = segment.Split('=', 2);
                var key = Uri.UnescapeDataString(parts[0]);
                var value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                queryParameters[key] = value;
            }
        }

        queryParameters["token"] = sharedToken.Trim();
        builder.Query = string.Join("&", queryParameters.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return builder.Uri;
    }

    [SupportedOSPlatform("browser")]
    private static partial class JSInterop
    {
        [JSImport("open", "BrowserWebSocketBridge")]
        public static partial Task OpenAsync(int connectionId, string url);

        [JSImport("close", "BrowserWebSocketBridge")]
        public static partial void Close(int connectionId, int code, string reason);

        [JSImport("sendByte", "BrowserWebSocketBridge")]
        public static partial void SendByte(int connectionId, int value);

        [JSImport("drainReceived", "BrowserWebSocketBridge")]
        public static partial string DrainReceived(int connectionId);

        [JSImport("getReadyState", "BrowserWebSocketBridge")]
        public static partial int GetReadyState(int connectionId);

        [JSImport("cleanup", "BrowserWebSocketBridge")]
        public static partial void Cleanup(int connectionId);
    }
}
