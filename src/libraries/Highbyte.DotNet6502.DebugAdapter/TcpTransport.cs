using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// TCP-based transport for Debug Adapter Protocol.
/// Reads from and writes to a TCP socket using the DAP message format.
/// </summary>
public class TcpTransport : IDebugAdapterTransport
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly StreamWriter _log;
    private bool _disposed;

    public event EventHandler? Disconnected;

    public TcpTransport(TcpClient client, StreamWriter log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _stream = client.GetStream();
        _log = log;
    }

    public async Task<JsonObject?> ReadMessageAsync()
    {
        try
        {
            // Read headers
            var headers = new Dictionary<string, string>();
            while (true)
            {
                var line = await ReadLineAsync(_stream);
                if (string.IsNullOrEmpty(line))
                    break;

                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                {
                    headers[parts[0].Trim()] = parts[1].Trim();
                }
            }

            if (!headers.ContainsKey("Content-Length"))
            {
                _log.WriteLine("[TCP Transport] No Content-Length header, connection closed");
                Disconnected?.Invoke(this, EventArgs.Empty);
                return null;
            }

            var contentLength = int.Parse(headers["Content-Length"]);
            
            // Read body
            var buffer = new byte[contentLength];
            var bytesRead = 0;
            while (bytesRead < contentLength)
            {
                var n = await _stream.ReadAsync(buffer, bytesRead, contentLength - bytesRead);
                if (n == 0)
                {
                    _log.WriteLine("[TCP Transport] Stream ended while reading body");
                    Disconnected?.Invoke(this, EventArgs.Empty);
                    return null;
                }
                bytesRead += n;
            }

            var json = Encoding.UTF8.GetString(buffer);
            _log.WriteLine($"[TCP Transport] Received: {json}");
            _log.Flush();

            return JsonSerializer.Deserialize<JsonObject>(json);
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[TCP Transport] Error reading message: {ex.Message}");
            _log.Flush();
            Disconnected?.Invoke(this, EventArgs.Empty);
            return null;
        }
    }

    public async Task SendMessageAsync(JsonObject message)
    {
        try
        {
            var json = message.ToJsonString();
            var bytes = Encoding.UTF8.GetBytes(json);
            
            var header = $"Content-Length: {bytes.Length}\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);

            await _stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await _stream.WriteAsync(bytes, 0, bytes.Length);
            await _stream.FlushAsync();

            _log.WriteLine($"[TCP Transport] Sent: {json}");
            _log.Flush();
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[TCP Transport] Error sending message: {ex.Message}");
            _log.Flush();
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<string> ReadLineAsync(NetworkStream stream)
    {
        var sb = new StringBuilder();
        while (true)
        {
            var b = stream.ReadByte();
            if (b == -1)
                return sb.ToString();
            
            var c = (char)b;
            if (c == '\n')
                return sb.ToString().TrimEnd('\r');
            
            sb.Append(c);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stream?.Dispose();
        _client?.Dispose();
    }
}
