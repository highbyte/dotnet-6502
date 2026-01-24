using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Handles Debug Adapter Protocol message reading/writing over stdin/stdout
/// </summary>
public class DapProtocol
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly StreamWriter _log;
    private int _sequenceNumber = 1;

    public DapProtocol(Stream input, Stream output, StreamWriter log)
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
                return null;

            var contentLength = int.Parse(headers["Content-Length"]);
            
            // Read body
            var buffer = new byte[contentLength];
            var bytesRead = 0;
            while (bytesRead < contentLength)
            {
                var n = await _input.ReadAsync(buffer, bytesRead, contentLength - bytesRead);
                if (n == 0)
                    return null;
                bytesRead += n;
            }

            var json = Encoding.UTF8.GetString(buffer);
            _log.WriteLine($"[DAP] Received: {json}");
            _log.Flush();

            return JsonSerializer.Deserialize<JsonObject>(json);
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[DAP] Error reading message: {ex}");
            _log.Flush();
            return null;
        }
    }

    public async Task SendMessageAsync(JsonObject message)
    {
        try
        {
            message["seq"] = _sequenceNumber++;
            
            var json = message.ToJsonString();
            var bytes = Encoding.UTF8.GetBytes(json);
            
            var header = $"Content-Length: {bytes.Length}\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);

            await _output.WriteAsync(headerBytes, 0, headerBytes.Length);
            await _output.WriteAsync(bytes, 0, bytes.Length);
            await _output.FlushAsync();

            _log.WriteLine($"[DAP] Sent: {json}");
            _log.Flush();
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[DAP] Error sending message: {ex}");
            _log.Flush();
        }
    }

    public async Task SendResponseAsync(int requestSeq, string command, JsonObject? body = null)
    {
        var response = new JsonObject
        {
            ["type"] = "response",
            ["request_seq"] = requestSeq,
            ["success"] = true,
            ["command"] = command
        };

        if (body != null)
        {
            response["body"] = body;
        }

        await SendMessageAsync(response);
    }

    public async Task SendEventAsync(string eventName, JsonObject? body = null)
    {
        var evt = new JsonObject
        {
            ["type"] = "event",
            ["event"] = eventName
        };

        if (body != null)
        {
            evt["body"] = body;
        }

        await SendMessageAsync(evt);
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
}
