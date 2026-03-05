using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Base class for DAP transports that share the same Content-Length framing protocol.
/// Subclasses provide the underlying streams and stream cleanup.
/// </summary>
public abstract class BaseTransport : IDebugAdapterTransport
{
    private readonly Stream _readStream;
    private readonly Stream _writeStream;
    private readonly StreamWriter _log;
    private readonly string _transportName;

    public event EventHandler? Disconnected;

    protected BaseTransport(Stream readStream, Stream writeStream, StreamWriter log, string transportName)
    {
        _readStream = readStream;
        _writeStream = writeStream;
        _log = log;
        _transportName = transportName;
    }

    public async Task<JsonObject?> ReadMessageAsync()
    {
        try
        {
            // Read headers (Content-Length: NNN followed by blank line)
            var headers = new Dictionary<string, string>();
            while (true)
            {
                var line = await ReadLineAsync(_readStream);
                if (string.IsNullOrEmpty(line))
                    break;

                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                    headers[parts[0].Trim()] = parts[1].Trim();
            }

            if (!headers.ContainsKey("Content-Length"))
            {
                _log.WriteLine($"[{_transportName} Transport] No Content-Length header, connection closed");
                Disconnected?.Invoke(this, EventArgs.Empty);
                return null;
            }

            var contentLength = int.Parse(headers["Content-Length"]);

            // Read body
            var buffer = new byte[contentLength];
            var bytesRead = 0;
            while (bytesRead < contentLength)
            {
                var n = await _readStream.ReadAsync(buffer, bytesRead, contentLength - bytesRead);
                if (n == 0)
                {
                    _log.WriteLine($"[{_transportName} Transport] Stream ended while reading body");
                    Disconnected?.Invoke(this, EventArgs.Empty);
                    return null;
                }
                bytesRead += n;
            }

            var json = Encoding.UTF8.GetString(buffer);
            _log.WriteLine($"[{_transportName} Transport] Received: {json}");
            _log.Flush();

            return JsonSerializer.Deserialize<JsonObject>(json);
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[{_transportName} Transport] Error reading message: {ex.Message}");
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

            await _writeStream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await _writeStream.WriteAsync(bytes, 0, bytes.Length);
            await _writeStream.FlushAsync();

            _log.WriteLine($"[{_transportName} Transport] Sent: {json}");
            _log.Flush();
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[{_transportName} Transport] Error sending message: {ex.Message}");
            _log.Flush();
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private static async Task<string> ReadLineAsync(Stream stream)
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

    public abstract void Dispose();
}
