namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// STDIO-based transport for Debug Adapter Protocol.
/// Reads from stdin and writes to stdout using the DAP message format.
/// </summary>
public class StdioTransport : BaseTransport
{
    /// <param name="input">Stream to read DAP messages from (typically stdin).</param>
    /// <param name="output">Stream to write DAP messages to (typically stdout).</param>
    /// <param name="log">Log writer for diagnostic messages.</param>
    public StdioTransport(Stream input, Stream output, StreamWriter log)
        : base(input, output, log, "STDIO")
    {
    }

    public override void Dispose()
    {
        // Streams are owned by the caller — do not dispose them here.
    }
}
