using System.Text;
using System.Text.Json.Nodes;
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.DebugAdapter;

/// <summary>
/// The debug adapter's view of a system's debug values, driven over the Debug Adapter Protocol
/// with a C64 behind it: the VIC-II scope and its variables, hover evaluation, and the Debug
/// Console's "run" command to a raster position.
/// </summary>
public class DebugAdapterSystemValuesTests : IDisposable
{
    private readonly C64 _c64;
    private readonly DebugAdapterLogic _adapter;
    private readonly StreamWriter _log = new(Stream.Null);
    private readonly MemoryStream _input = new();
    private readonly CapturingStream _output = new();
    private int _seq;

    /// <summary>Collects the adapter's DAP messages as they are written.</summary>
    private sealed class CapturingStream : MemoryStream
    {
        private readonly object _lock = new();
        private readonly StringBuilder _text = new();
        public List<JsonObject> Messages { get; } = new();
        public event Action? MessageWritten;

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                _text.Append(Encoding.UTF8.GetString(buffer, offset, count));
                ParseMessages();
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.ToArray(), 0, buffer.Length);
            return ValueTask.CompletedTask;
        }

        private void ParseMessages()
        {
            while (true)
            {
                var s = _text.ToString();
                var headerEnd = s.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEnd < 0)
                    return;
                var length = int.Parse(s[..headerEnd].Split(':')[1].Trim());
                var bodyStart = headerEnd + 4;
                if (Encoding.UTF8.GetByteCount(s[bodyStart..]) < length)
                    return;
                var bodyChars = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(s[bodyStart..])[..length]).Length;
                var body = s.Substring(bodyStart, bodyChars);
                _text.Remove(0, bodyStart + bodyChars);
                Messages.Add((JsonObject)JsonNode.Parse(body)!);
                MessageWritten?.Invoke();
            }
        }
    }

    public DebugAdapterSystemValuesTests()
    {
        _c64 = C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);
        for (var address = 0x1000; address < 0x1100; address++)
            _c64.Mem[(ushort)address] = 0xEA;
        _c64.Mem.StoreData(0x1100, [0x4C, 0x00, 0x10]);
        _c64.CPU.PC = 0x1000;

        var transport = new StdioTransport(_input, _output, _log);
        var protocol = new DapProtocol(transport, _log);
        _adapter = new DebugAdapterLogic(protocol, _log, _c64, initiallyPaused: true, builtInExecution: true);
    }

    public void Dispose()
    {
        _log.Dispose();
        _input.Dispose();
        _output.Dispose();
    }

    private async Task<JsonObject> RequestAsync(string command, JsonObject? arguments = null)
    {
        var seq = ++_seq;
        await _adapter.HandleMessageAsync(new JsonObject
        {
            ["seq"] = seq,
            ["type"] = "request",
            ["command"] = command,
            ["arguments"] = arguments ?? new JsonObject(),
        });
        return _output.Messages.Single(m => m["type"]?.ToString() == "response" && m["request_seq"]?.GetValue<int>() == seq);
    }

    private async Task<string> EvaluateAsync(string expression, string context = "repl")
    {
        var response = await RequestAsync("evaluate", new JsonObject { ["expression"] = expression, ["context"] = context });
        return response["body"]!["result"]!.ToString();
    }

    private async Task<JsonObject> WaitForEventAsync(string name, int timeoutMs = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var found = _output.Messages.FirstOrDefault(m => m["type"]?.ToString() == "event" && m["event"]?.ToString() == name);
            if (found != null)
                return found;
            await Task.Delay(10);
        }
        throw new TimeoutException($"No '{name}' event. Messages: {string.Join("\n", _output.Messages.Select(m => m.ToJsonString()))}");
    }

    [Fact]
    public async Task The_vic2_scope_lists_the_position_values()
    {
        var scopes = (await RequestAsync("scopes", new JsonObject { ["frameId"] = 1 }))["body"]!["scopes"]!.AsArray();
        var vic2 = scopes.Single(s => s!["name"]!.ToString() == "VIC-II")!;
        var reference = vic2["variablesReference"]!.GetValue<int>();

        _c64.Vic2.AdvanceRaster(52 * 63 + 24);
        var variables = (await RequestAsync("variables", new JsonObject { ["variablesReference"] = reference }))["body"]!["variables"]!.AsArray();

        Assert.Equal(["RASTER", "CYCLE", "FRAMECYCLE", "FRAME"], variables.Select(v => v!["name"]!.ToString()));
        Assert.Equal(["52", "25", (52 * 63 + 24).ToString(), "0"], variables.Select(v => v!["value"]!.ToString()));

        var set = await RequestAsync("setVariable", new JsonObject { ["variablesReference"] = reference, ["name"] = "RASTER", ["value"] = "1" });
        Assert.False(set["success"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Values_evaluate_on_hover_and_in_the_console()
    {
        _c64.Vic2.AdvanceRaster(100 * 63 + 5);
        Assert.Equal("100", await EvaluateAsync("RASTER", context: "hover"));
        Assert.Equal("6", await EvaluateAsync("cycle"));
        Assert.Equal("$1000", await EvaluateAsync("PC"));          // registers unchanged
        Assert.Equal("not available", await EvaluateAsync("BORDER"));
    }

    [Fact]
    public async Task Run_lists_the_raster_target_and_rejects_bad_positions()
    {
        var usage = await EvaluateAsync("run");
        Assert.Contains("run until <condition>", usage);
        Assert.Contains("run raster <line> [cycle]", usage);
        Assert.Contains("Raster line must be 0 to 311", await EvaluateAsync("run raster 400"));
        Assert.Contains("Usage: raster <line> [cycle]", await EvaluateAsync("run raster"));
        Assert.Contains("Unknown run target 'sprite'", await EvaluateAsync("run sprite 1"));
        Assert.Contains("Invalid condition", await EvaluateAsync("run until BORDER == 1"));
        Assert.True(_adapter.IsStopped);
    }

    [Fact]
    public async Task Run_raster_resumes_and_stops_at_the_position()
    {
        Assert.StartsWith("Running until", await EvaluateAsync("run raster 100 20"));
        Assert.False(_adapter.IsStopped);
        await WaitForEventAsync("continued");

        var stopped = await WaitForEventAsync("stopped");

        Assert.Equal("step", stopped["body"]!["reason"]!.ToString());
        Assert.True(_adapter.IsStopped);
        Assert.Contains(_output.Messages, m => m["event"]?.ToString() == "output" && m["body"]!["output"]!.ToString().StartsWith("Condition '"));
        Assert.Equal("100", await EvaluateAsync("RASTER"));
        Assert.Contains(await EvaluateAsync("CYCLE"), new[] { "20", "21" });
    }

    [Fact]
    public async Task Run_until_a_register_condition_stops_when_it_holds()
    {
        _c64.Mem[0x1000] = 0xE8;   // INX in place of the first NOP: X counts the loops
        Assert.StartsWith("Running until", await EvaluateAsync("run until X == 3"));
        await WaitForEventAsync("stopped");
        Assert.Equal("$03", await EvaluateAsync("X"));
    }
}
