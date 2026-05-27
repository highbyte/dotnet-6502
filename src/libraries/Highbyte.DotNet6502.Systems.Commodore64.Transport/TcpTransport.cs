using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.Transport;

public sealed class TcpTransport : ISwiftLinkTransport
{
    private static readonly TimeSpan ReaderShutdownTimeout = TimeSpan.FromSeconds(1);

    private readonly string _host;
    private readonly int _port;
    private readonly ConcurrentQueue<byte> _receivedBytes = new();
    private readonly object _sync = new();

    private readonly ILogger _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;
    private bool _isConnected;
    private bool _warnedDisconnectedSend;

    public TcpTransport(string host, int port, ILogger logger)
    {
        _host = host;
        _port = port;
        _logger = logger;
    }

    public bool IsConnected => _isConnected;
    public bool IsCarrierDetected => _isConnected;
    public bool IsDataSetReady => _isConnected;

    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        _logger.LogInformation("Connecting SwiftLink TCP transport to {Host}:{Port}.", _host, _port);
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(_host, _port, cancellationToken);
        var stream = tcpClient.GetStream();
        var readerCts = new CancellationTokenSource();

        lock (_sync)
        {
            _tcpClient = tcpClient;
            _stream = stream;
            _readerCts = readerCts;
            _readerTask = Task.Run(() => ReaderLoopAsync(stream, readerCts.Token), CancellationToken.None);
            _isConnected = true;
            _warnedDisconnectedSend = false;
        }

        _logger.LogInformation("Connected SwiftLink TCP transport to {Host}:{Port}.", _host, _port);
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        Task? readerTask;
        CancellationTokenSource? readerCts;
        NetworkStream? stream;
        TcpClient? tcpClient;

        lock (_sync)
        {
            readerTask = _readerTask;
            readerCts = _readerCts;
            stream = _stream;
            tcpClient = _tcpClient;
            _readerTask = null;
            _readerCts = null;
            _stream = null;
            _tcpClient = null;
            _isConnected = false;
        }

        readerCts?.Cancel();
        TryShutdownSocket(tcpClient);

        if (stream != null)
            await stream.DisposeAsync();
        tcpClient?.Dispose();

        if (readerTask != null)
        {
            try
            {
                var waitToken = cancellationToken;
                if (!waitToken.CanBeCanceled)
                {
                    using var waitCts = new CancellationTokenSource(ReaderShutdownTimeout);
                    await readerTask.WaitAsync(waitCts.Token);
                }
                else
                {
                    await readerTask.WaitAsync(waitToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Timed out waiting for SwiftLink TCP transport reader loop to stop for {Host}:{Port}.",
                    _host,
                    _port);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
        }

        readerCts?.Dispose();
        Reset();
        _logger.LogInformation("Disconnected SwiftLink TCP transport from {Host}:{Port}.", _host, _port);
    }

    public bool TryDequeueReceivedByte(out byte value)
        => _receivedBytes.TryDequeue(out value);

    public async ValueTask SendAsync(byte value, CancellationToken cancellationToken = default)
    {
        var stream = _stream;
        if (stream == null || !_isConnected)
        {
            if (!_warnedDisconnectedSend)
            {
                _logger.LogWarning("SwiftLink send attempted while TCP transport is not connected.");
                _warnedDisconnectedSend = true;
            }
            return;
        }

        _logger.LogDebug("SwiftLink TCP transport sending byte 0x{Value:X2}.", value);
        await stream.WriteAsync(new[] { value }, cancellationToken);
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

    private async Task ReaderLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (bytesRead <= 0)
            {
                _logger.LogInformation("SwiftLink TCP transport remote closed the connection.");
                lock (_sync)
                {
                    _isConnected = false;
                }
                break;
            }

            _logger.LogDebug(
                "SwiftLink TCP transport received {ByteCount} byte(s): {HexBytes} |{Ascii}|",
                bytesRead,
                FormatHexBytes(buffer, bytesRead),
                FormatAsciiBytes(buffer, bytesRead));

            for (var i = 0; i < bytesRead; i++)
                _receivedBytes.Enqueue(buffer[i]);
        }
    }

    private void TryShutdownSocket(TcpClient? tcpClient)
    {
        if (tcpClient?.Client == null)
            return;

        try
        {
            tcpClient.Client.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static string FormatHexBytes(byte[] buffer, int bytesRead)
    {
        var builder = new StringBuilder(bytesRead * 3);
        for (var i = 0; i < bytesRead; i++)
        {
            if (i > 0)
                builder.Append(' ');
            builder.Append(buffer[i].ToString("X2"));
        }
        return builder.ToString();
    }

    private static string FormatAsciiBytes(byte[] buffer, int bytesRead)
    {
        var builder = new StringBuilder(bytesRead);
        for (var i = 0; i < bytesRead; i++)
        {
            var value = buffer[i];
            builder.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
        }
        return builder.ToString();
    }
}
