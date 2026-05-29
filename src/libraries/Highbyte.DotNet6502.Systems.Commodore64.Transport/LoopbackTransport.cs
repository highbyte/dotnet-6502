using System.Collections.Concurrent;

namespace Highbyte.DotNet6502.Systems.Commodore64.Transport;

public sealed class LoopbackTransport : ISwiftLinkTransport
{
    private readonly ConcurrentQueue<byte> _receivedBytes = new();

    public bool IsConnected { get; private set; }
    public bool IsCarrierDetected => IsConnected;
    public bool IsDataSetReady => IsConnected;

    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        Reset();
        return ValueTask.CompletedTask;
    }

    public bool TryDequeueReceivedByte(out byte value)
        => _receivedBytes.TryDequeue(out value);

    public ValueTask SendAsync(byte value, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            _receivedBytes.Enqueue(value);
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
        IsConnected = false;
        Reset();
    }
}
