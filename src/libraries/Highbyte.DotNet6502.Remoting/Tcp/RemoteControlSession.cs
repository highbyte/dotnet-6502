using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Highbyte.DotNet6502.Remoting.Protocol;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Remoting.Tcp;

/// <summary>
/// Handles the lifecycle of one persistent remote control connection.
/// Reads newline-delimited JSON requests, dispatches via <see cref="RemoteCommandDispatcher"/>,
/// and writes newline-delimited JSON responses.
/// </summary>
public class RemoteControlSession
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly TcpClient _client;
    private readonly RemoteCommandDispatcher _dispatcher;
    private readonly ILogger<RemoteControlSession>? _logger;

    public RemoteControlSession(TcpClient client, RemoteCommandDispatcher dispatcher, ILoggerFactory? loggerFactory = null)
    {
        _client = client;
        _dispatcher = dispatcher;
        _logger = loggerFactory?.CreateLogger<RemoteControlSession>();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _logger?.LogInformation("[Session] Client session started");
        try
        {
            using var stream = _client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };

            while (!ct.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("[Session] Read ended: {Message}", ex.Message);
                    break;
                }

                if (line == null) break; // Client closed connection

                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                RemoteCommandResult result;
                try
                {
                    var cmd = JsonSerializer.Deserialize<RemoteCommand>(line, s_jsonOptions);
                    result = cmd != null
                        ? await _dispatcher.DispatchAsync(cmd)
                        : new RemoteCommandResult { Ok = false, Error = "Null command" };
                }
                catch (JsonException ex)
                {
                    result = new RemoteCommandResult { Ok = false, Error = $"JSON parse error: {ex.Message}" };
                }
                catch (Exception ex)
                {
                    result = new RemoteCommandResult { Ok = false, Error = ex.Message };
                }

                try
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(result, s_jsonOptions));
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("[Session] Write ended: {Message}", ex.Message);
                    break;
                }
            }
        }
        finally
        {
            try { _client.Close(); } catch { }
            _logger?.LogInformation("[Session] Client session ended");
        }
    }
}
