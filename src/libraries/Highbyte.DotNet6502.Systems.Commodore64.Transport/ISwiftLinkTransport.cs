namespace Highbyte.DotNet6502.Systems.Commodore64.Transport;

public interface ISwiftLinkTransport : IDisposable
{
    bool IsConnected { get; }
    bool IsCarrierDetected { get; }
    bool IsDataSetReady { get; }
    ValueTask ConnectAsync(CancellationToken cancellationToken = default);
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
    bool TryDequeueReceivedByte(out byte value);
    ValueTask SendAsync(byte value, CancellationToken cancellationToken = default);
    void Reset();
}
