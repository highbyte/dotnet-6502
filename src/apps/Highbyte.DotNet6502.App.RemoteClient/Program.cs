using System.Net.Sockets;
using System.Text;
using System.Text.Json;

// Remote client for the DotNet 6502 emulator TCP remote control server.
// Usage: dotnet-6502-remote [--port <port>] [--host <host>] <command> [params...]
// Usage: dotnet-6502-remote --help

const int DefaultPort = 6510;
const string DefaultHost = "127.0.0.1";

// Command definitions for --help output and self-description
var commands = new[]
{
    ("emu.state",     "",                           "Get emulator state and system name"),
    ("emu.start",     "",                           "Start the emulator"),
    ("emu.stop",      "",                           "Stop the emulator"),
    ("emu.pause",     "",                           "Pause the emulator"),
    ("emu.reset",     "",                           "Reset the emulator"),
    ("emu.quit",      "",                           "Quit the emulator (headless only)"),
    ("cpu.get",       "",                           "Get CPU registers"),
    ("mem.read",      "--addr <hex> --len <int>",   "Read bytes from memory"),
    ("mem.write",     "--addr <hex> --data <b,b,..>","Write bytes to memory"),
    ("joystick.set",       "--port <1|2> [--up] [--down] [--left] [--right] [--fire]", "Set joystick state"),
    ("keyboard.press",     "--key <keyname>",              "Press (hold) a named key"),
    ("keyboard.release",   "--key <keyname>",              "Release a previously pressed key"),
    ("keyboard.releaseall","",                             "Release all injected keys"),
    ("keyboard.iskeydown", "--key <keyname>",              "Check if a key is currently down"),
    ("keyboard.getall",    "",                             "List all valid key names for the current system"),
    ("c64.type",           "--text <string>",             "Paste text into C64 keyboard buffer (C64 only)"),
    ("c64.isbasicstarted", "",                             "Check if C64 BASIC has finished initializing (C64 only)"),
    ("c64.getbasicsource", "",                             "Get the current BASIC program as text (C64 only)"),
    ("screenshot",    "[--output <file.png>]",      "Capture screenshot (Base64 PNG or saved to file)"),
    ("ui.message",    "--text <string> [--level info|warning|error]", "Display message in emulator UI"),
};

if (args.Contains("--help") || args.Contains("-h") || args.Length == 0)
{
    PrintHelp();
    return 0;
}

int port = DefaultPort;
string host = DefaultHost;
int cmdIndex = 0;

// Parse global options
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--port" && i + 1 < args.Length)
    {
        if (!int.TryParse(args[i + 1], out port))
        {
            Console.Error.WriteLine($"Invalid port: {args[i + 1]}");
            return 2;
        }
        i++;
    }
    else if (args[i] == "--host" && i + 1 < args.Length)
    {
        host = args[i + 1];
        i++;
    }
    else
    {
        cmdIndex = i;
        break;
    }
}

if (cmdIndex >= args.Length)
{
    Console.Error.WriteLine("No command specified. Use --help for usage.");
    return 2;
}

// Build the JSON request from the command and remaining args
var cmdArgs = args[cmdIndex..];
var requestObj = BuildRequest(cmdArgs, out string? outputFile);
if (requestObj == null)
{
    Console.Error.WriteLine($"Unknown command: {cmdArgs[0]}. Use --help for usage.");
    return 2;
}

var requestJson = JsonSerializer.Serialize(requestObj);

// Connect and send
try
{
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(host, port);
    using var stream = tcp.GetStream();
    using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

    await writer.WriteLineAsync(requestJson);

    var responseLine = await reader.ReadLineAsync();
    if (responseLine == null)
    {
        Console.Error.WriteLine("No response from server.");
        return 1;
    }

    using var doc = JsonDocument.Parse(responseLine);
    var root = doc.RootElement;

    bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
    if (!ok)
    {
        var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Unknown error";
        Console.Error.WriteLine($"Error: {error}");
        return 1;
    }

    // Handle screenshot specially: save to file or print base64
    if (cmdArgs[0] == "screenshot")
    {
        if (root.TryGetProperty("data", out var dataProp))
        {
            var b64 = dataProp.GetString();
            if (outputFile != null && b64 != null)
            {
                var bytes = Convert.FromBase64String(b64);
                await File.WriteAllBytesAsync(outputFile, bytes);
                Console.WriteLine($"Screenshot saved to {outputFile}");
            }
            else
            {
                Console.WriteLine(b64);
            }
        }
        return 0;
    }

    // Print the response pretty-printed
    Console.WriteLine(JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (SocketException ex)
{
    Console.Error.WriteLine($"Could not connect to {host}:{port} — {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

Dictionary<string, object?>? BuildRequest(string[] cmdArgs, out string? outputFile)
{
    outputFile = null;
    if (cmdArgs.Length == 0) return null;

    var cmd = cmdArgs[0];
    var p = ParseParams(cmdArgs[1..]);

    var req = new Dictionary<string, object?> { ["id"] = 1, ["cmd"] = cmd };

    switch (cmd)
    {
        case "emu.state": case "emu.start": case "emu.stop":
        case "emu.pause": case "emu.reset": case "emu.quit":
        case "cpu.get":
            break;

        case "mem.read":
            if (p.TryGetValue("addr", out var addr)) req["addr"] = addr;
            if (p.TryGetValue("len", out var len) && int.TryParse(len, out int lenInt)) req["len"] = lenInt;
            break;

        case "mem.write":
            if (p.TryGetValue("addr", out var wAddr)) req["addr"] = wAddr;
            if (p.TryGetValue("data", out var data))
                req["data"] = data!.Split(',').Select(b => int.Parse(b.Trim())).ToArray();
            break;

        case "joystick.set":
            if (p.TryGetValue("port", out var jp) && int.TryParse(jp, out int jport)) req["port"] = jport;
            if (p.ContainsKey("up"))    req["up"]    = true;
            if (p.ContainsKey("down"))  req["down"]  = true;
            if (p.ContainsKey("left"))  req["left"]  = true;
            if (p.ContainsKey("right")) req["right"] = true;
            if (p.ContainsKey("fire"))  req["fire"]  = true;
            break;

        case "keyboard.press":
        case "keyboard.release":
        case "keyboard.iskeydown":
            if (p.TryGetValue("key", out var key)) req["key"] = key;
            break;

        case "keyboard.releaseall":
        case "keyboard.getall":
            break;

        case "c64.type":
            if (p.TryGetValue("text", out var text)) req["text"] = text;
            break;

        case "c64.isbasicstarted":
        case "c64.getbasicsource":
            break;

        case "screenshot":
            if (p.TryGetValue("output", out var outFile)) outputFile = outFile;
            break;

        case "ui.message":
            if (p.TryGetValue("text", out var msg)) req["text"] = msg;
            if (p.TryGetValue("level", out var level)) req["level"] = level;
            break;

        default:
            return null;
    }

    return req;
}

Dictionary<string, string?> ParseParams(string[] args)
{
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].StartsWith("--"))
        {
            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                result[key] = args[i + 1];
                i++;
            }
            else
            {
                result[key] = null; // flag with no value
            }
        }
    }
    return result;
}

void PrintHelp()
{
    Console.WriteLine("dotnet-6502-remote — Remote control client for the DotNet 6502 Emulator");
    Console.WriteLine();
    Console.WriteLine("USAGE:");
    Console.WriteLine("  dotnet-6502-remote [--host <host>] [--port <port>] <command> [parameters]");
    Console.WriteLine();
    Console.WriteLine("OPTIONS:");
    Console.WriteLine("  --host <host>   Server hostname or IP (default: 127.0.0.1)");
    Console.WriteLine($"  --port <port>   TCP port (default: {DefaultPort})");
    Console.WriteLine("  --help          Show this help");
    Console.WriteLine();
    Console.WriteLine("COMMANDS:");
    int maxCmd = commands.Max(c => c.Item1.Length);
    foreach (var (name, paramDesc, desc) in commands)
    {
        var padding = new string(' ', maxCmd - name.Length + 2);
        Console.WriteLine($"  {name}{padding}{desc}");
        if (!string.IsNullOrEmpty(paramDesc))
            Console.WriteLine($"  {new string(' ', maxCmd + 2)}  Parameters: {paramDesc}");
    }
    Console.WriteLine();
    Console.WriteLine("EXAMPLES:");
    Console.WriteLine("  dotnet-6502-remote emu.state");
    Console.WriteLine("  dotnet-6502-remote --port 6510 emu.start");
    Console.WriteLine("  dotnet-6502-remote mem.read --addr C000 --len 16");
    Console.WriteLine("  dotnet-6502-remote mem.write --addr C000 --data 169,0,133,208");
    Console.WriteLine("  dotnet-6502-remote joystick.set --port 1 --up --fire");
    Console.WriteLine("  dotnet-6502-remote keyboard.press --key space");
    Console.WriteLine("  dotnet-6502-remote keyboard.release --key space");
    Console.WriteLine("  dotnet-6502-remote keyboard.releaseall");
    Console.WriteLine("  dotnet-6502-remote keyboard.iskeydown --key return");
    Console.WriteLine("  dotnet-6502-remote keyboard.getall");
    Console.WriteLine("  dotnet-6502-remote c64.type --text \"LOAD\\\"*\\\",8,1\"");
    Console.WriteLine("  dotnet-6502-remote c64.isbasicstarted");
    Console.WriteLine("  dotnet-6502-remote c64.getbasicsource");
    Console.WriteLine("  dotnet-6502-remote screenshot --output /tmp/screen.png");
    Console.WriteLine("  dotnet-6502-remote ui.message --text \"Hello from remote\" --level info");
}
