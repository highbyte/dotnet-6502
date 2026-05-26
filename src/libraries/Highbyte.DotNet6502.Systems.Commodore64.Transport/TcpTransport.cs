using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Highbyte.DotNet6502.Systems.Commodore64.Transport;

public sealed class TcpTransport : ISwiftLinkTransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly ConcurrentQueue<byte> _receivedBytes = new();
    private readonly object _sync = new();

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;

    public TcpTransport(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public bool IsConnected => _tcpClient?.Connected == true;

    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

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
        }
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
        }

        readerCts?.Cancel();

        if (stream != null)
            await stream.DisposeAsync();
        tcpClient?.Dispose();

        if (readerTask != null)
        {
            try
            {
                await readerTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
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
    }

    public bool TryDequeueReceivedByte(out byte value)
        => _receivedBytes.TryDequeue(out value);

    public async ValueTask SendAsync(byte value, CancellationToken cancellationToken = default)
    {
        var stream = _stream;
        if (stream == null)
            return;

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
                break;

            for (var i = 0; i < bytesRead; i++)
                _receivedBytes.Enqueue(buffer[i]);
        }
    }
}
