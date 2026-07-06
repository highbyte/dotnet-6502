using System.Text.Json;
using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Remoting.Protocol;

/// <summary>
/// Newline-delimited JSON request from a remote client.
/// Unknown fields are ignored; only populated fields carry meaning per command.
/// </summary>
public class RemoteCommand
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = "";

    // mem.read / mem.write
    [JsonPropertyName("addr")]
    public string? Addr { get; set; }

    [JsonPropertyName("len")]
    public int? Len { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }

    // joystick.set / joystick.press / joystick.release / joystick.releaseall
    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("up")]
    public bool? Up { get; set; }

    [JsonPropertyName("down")]
    public bool? Down { get; set; }

    [JsonPropertyName("left")]
    public bool? Left { get; set; }

    [JsonPropertyName("right")]
    public bool? Right { get; set; }

    [JsonPropertyName("fire")]
    public bool? Fire { get; set; }

    // keyboard.press / keyboard.release / keyboard.iskeydown
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    // c64.type / ui.message
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("level")]
    public string? Level { get; set; }

    // cpu.set
    [JsonPropertyName("pc")]
    public string? PC { get; set; }

    [JsonPropertyName("a")]
    public int? A { get; set; }

    [JsonPropertyName("x")]
    public int? X { get; set; }

    [JsonPropertyName("y")]
    public int? Y { get; set; }

    [JsonPropertyName("sp")]
    public int? SP { get; set; }

    [JsonPropertyName("flags")]
    public string? Flags { get; set; }

    // emu.selectsystem / emu.selectvariant
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    // emu.savesnapshot / emu.loadsnapshot — server-side file path. Absolute paths are used as-is;
    // relative paths resolve under the emulator host's shared snapshot directory.
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    // emu.runframes — number of frames to step (default 1).
    [JsonPropertyName("count")]
    public int? Count { get; set; }
}
