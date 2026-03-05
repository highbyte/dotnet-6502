using System.Text.Json.Nodes;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Handles Debug Adapter Protocol message formatting and convenience methods.
/// Uses an IDebugAdapterTransport for the actual I/O.
/// </summary>
public class DapProtocol
{
    private readonly IDebugAdapterTransport _transport;
    private readonly StreamWriter _log;
    private int _sequenceNumber = 1;

    public DapProtocol(IDebugAdapterTransport transport, StreamWriter log)
    {
        _transport = transport;
        _log = log;
    }

    public async Task<JsonObject?> ReadMessageAsync()
    {
        return await _transport.ReadMessageAsync();
    }

    public async Task SendMessageAsync(JsonObject message)
    {
        // Add sequence number to all messages
        message["seq"] = _sequenceNumber++;
        
        await _transport.SendMessageAsync(message);
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

    public async Task SendErrorResponseAsync(int requestSeq, string command, string message)
    {
        var response = new JsonObject
        {
            ["type"] = "response",
            ["request_seq"] = requestSeq,
            ["success"] = false,
            ["command"] = command,
            ["message"] = message
        };

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
}
