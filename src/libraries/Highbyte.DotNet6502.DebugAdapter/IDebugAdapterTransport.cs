using System.Text.Json.Nodes;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Abstraction for different transport mechanisms (STDIO, TCP, etc.) used by the Debug Adapter Protocol.
/// </summary>
public interface IDebugAdapterTransport : IDisposable
{
    /// <summary>
    /// Reads a DAP message from the transport.
    /// Returns null if the connection is closed.
    /// </summary>
    Task<JsonObject?> ReadMessageAsync();

    /// <summary>
    /// Sends a DAP message through the transport.
    /// </summary>
    Task SendMessageAsync(JsonObject message);

    /// <summary>
    /// Event fired when the transport is disconnected.
    /// </summary>
    event EventHandler? Disconnected;
}
