using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// STDIO-based transport for Debug Adapter Protocol.
/// Reads from stdin and writes to stdout using the DAP message format.
/// </summary>
public class StdioTransport : IDebugAdapterTransport
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly StreamWriter _log;

    public event EventHandler? Disconnected;

    public StdioTransport(Stream input, Stream output, StreamWriter log)
    {
        _input = input;
        _output = output;
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
                var line = await ReadLineAsync(_input);
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
                Disconnected?.Invoke(this, EventArgs.Empty);
                return null;
            }

            var contentLength = int.Parse(headers["Content-Length"]);
            
            // Read body
            var buffer = new byte[contentLength];
            var bytesRead = 0;
            while (bytesRead < contentLength)
            {
                var n = await _input.ReadAsync(buffer, bytesRead, contentLength - bytesRead);
                if (n == 0)
                {
                    Disconnected?.Invoke(this, EventArgs.Empty);
                    return null;
                }
                bytesRead += n;
            }

            var json = Encoding.UTF8.GetString(buffer);
            _log.WriteLine($"[STDIO Transport] Received: {json}");
            _log.Flush();

            return JsonSerializer.Deserialize<JsonObject>(json);
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[STDIO Transport] Error reading message: {ex}");
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

            await _output.WriteAsync(headerBytes, 0, headerBytes.Length);
            await _output.WriteAsync(bytes, 0, bytes.Length);
            await _output.FlushAsync();

            _log.WriteLine($"[STDIO Transport] Sent: {json}");
            _log.Flush();
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[STDIO Transport] Error sending message: {ex}");
            _log.Flush();
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<string> ReadLineAsync(Stream stream)
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
        // Streams are owned by caller, don't dispose them
    }
}
