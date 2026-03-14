using System.Net.Sockets;
using System.Text;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Tracks open <see cref="LuaTcpConnection"/> instances and disposes them when the scripting
/// engine is re-initialised (e.g. on script reload).
/// </summary>
public sealed class LuaTcpProxy : IDisposable
{
    private readonly List<LuaTcpConnection> _connections = new();
    private bool _disposed;

    /// <summary>
    /// Opens an outbound TCP connection to <paramref name="host"/>:<paramref name="port"/>.
    /// Throws on failure so the caller can build a Lua error table.
    /// </summary>
    internal async Task<LuaTcpConnection> ConnectAsync(string host, int port, int timeoutMs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await LuaTcpConnection.ConnectAsync(host, port, timeoutMs);
    }

    /// <summary>Registers a connection so it is disposed when this proxy is disposed.</summary>
    internal void Track(LuaTcpConnection connection) => _connections.Add(connection);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var conn in _connections)
            conn.Dispose();
        _connections.Clear();
    }
}

/// <summary>
/// Wraps a single <see cref="TcpClient"/> / <see cref="NetworkStream"/> pair and exposes
/// async send / receive operations for use from Lua scripts.
/// </summary>
public sealed class LuaTcpConnection : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private bool _disposed;

    private LuaTcpConnection(TcpClient client, NetworkStream stream)
    {
        _client = client;
        _stream = stream;
    }

    /// <summary>
    /// Connects to <paramref name="host"/>:<paramref name="port"/> with an optional timeout.
    /// Throws <see cref="TimeoutException"/> or <see cref="SocketException"/> on failure.
    /// </summary>
    internal static async Task<LuaTcpConnection> ConnectAsync(string host, int port, int timeoutMs)
    {
        var client = new TcpClient();
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await client.ConnectAsync(host, port, cts.Token);
        }
        catch (OperationCanceledException)
        {
            client.Dispose();
            throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMs}ms.");
        }
        catch
        {
            client.Dispose();
            throw;
        }
        return new LuaTcpConnection(client, client.GetStream());
    }

    /// <summary>
    /// Sends <paramref name="data"/> over the connection.
    /// Returns (true, null) on success or (false, error message) on failure.
    /// </summary>
    internal async Task<(bool ok, string? error)> SendAsync(byte[] data)
    {
        if (_disposed) return (false, "Connection is closed.");
        try
        {
            await _stream.WriteAsync(data);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Receives exactly <paramref name="count"/> bytes.
    /// Loops until all bytes arrive or the stream closes.
    /// Returns (true, bytes, null) on success or (false, null, error) on failure.
    /// </summary>
    internal async Task<(bool ok, byte[]? data, string? error)> RecvNAsync(int count)
    {
        if (_disposed) return (false, null, "Connection is closed.");
        if (count <= 0) return (true, Array.Empty<byte>(), null);
        try
        {
            var buffer = new byte[count];
            int received = 0;
            while (received < count)
            {
                int n = await _stream.ReadAsync(buffer.AsMemory(received, count - received));
                if (n == 0)
                    return (false, null, "Connection closed before all bytes were received.");
                received += n;
            }
            return (true, buffer, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Receives bytes until a newline (<c>\n</c>) is encountered.
    /// Returns the line as a string with the trailing newline (and optional <c>\r</c>) stripped.
    /// Returns (true, line, null) on success or (false, null, error) on failure.
    /// </summary>
    internal async Task<(bool ok, string? data, string? error)> RecvLineAsync()
    {
        if (_disposed) return (false, null, "Connection is closed.");
        try
        {
            var lineBytes = new List<byte>(64);
            var oneByte = new byte[1];
            while (true)
            {
                int n = await _stream.ReadAsync(oneByte.AsMemory(0, 1));
                if (n == 0)
                {
                    // Stream closed — return whatever we have, or error if nothing
                    if (lineBytes.Count == 0)
                        return (false, null, "Connection closed before newline was received.");
                    break;
                }
                if (oneByte[0] == (byte)'\n')
                    break;
                lineBytes.Add(oneByte[0]);
            }
            // Strip trailing \r for Windows-style CRLF
            if (lineBytes.Count > 0 && lineBytes[lineBytes.Count - 1] == (byte)'\r')
                lineBytes.RemoveAt(lineBytes.Count - 1);
            return (true, Encoding.UTF8.GetString(lineBytes.ToArray()), null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stream.Dispose();
        _client.Dispose();
    }
}
