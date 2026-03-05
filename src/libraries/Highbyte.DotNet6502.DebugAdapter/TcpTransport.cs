using System.Net.Sockets;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// TCP-based transport for Debug Adapter Protocol.
/// Reads from and writes to a TCP socket using the DAP message format.
/// </summary>
public class TcpTransport : BaseTransport
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private bool _disposed;

    public TcpTransport(TcpClient client, StreamWriter log)
        : base(
            readStream: (client ?? throw new ArgumentNullException(nameof(client))).GetStream(),
            writeStream: client.GetStream(),
            log: log,
            transportName: "TCP")
    {
        _client = client;
        _stream = client.GetStream(); // same NetworkStream instance returned each time
    }

    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stream?.Dispose();
        _client?.Dispose();
    }
}
