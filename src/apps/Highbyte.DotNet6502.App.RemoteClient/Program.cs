using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Highbyte.DotNet6502.App.RemoteClient;

// Remote client for the DotNet 6502 emulator TCP remote control server.
// Usage: dotnet-6502-remote [--port <port>] [--host <host>] <command> [params...]
// Usage: dotnet-6502-remote --help

const int DefaultPort = 6510;
const string DefaultHost = "127.0.0.1";

// Command definitions for --help output and self-description
var commands = new[]
{
    ("emu.state",        "",                              "Get emulator state, system, and variant"),
    ("emu.start",        "",                              "Start (or resume from paused) the emulator"),
    ("emu.stop",         "",                              "Stop the emulator"),
    ("emu.pause",        "",                              "Pause the emulator"),
    ("emu.reset",        "",                              "Reset the emulator"),
    ("emu.quit",         "",                              "Quit the emulator (headless only)"),
    ("emu.systems",      "",                              "List available system names"),
    ("emu.selectsystem", "--name <system>",               "Select a system (emulator must be stopped)"),
    ("emu.variants",     "",                              "List config variants for the current system"),
    ("emu.selectvariant","--name <variant>",              "Select a config variant (emulator must be stopped)"),
    ("cpu.get",          "",                              "Get CPU registers"),
    ("cpu.set",          "[--pc <hex>] [--a <0-255>] [--x <0-255>] [--y <0-255>] [--sp <0-255>] [--flags <NVUBDIZC>]", "Set CPU registers"),
    ("mem.read",         "--addr <hex> --len <int>",                          "Read bytes from memory"),
    ("mem.write",        "--addr <hex> --data <b,b,..>",                       "Write bytes to memory"),
    ("mem.loadbin",      "--addr <hex> --file <path> | --data <base64>",       "Load binary file into memory at a specific address"),
    ("joystick.set",       "--port <1|2> [--up|--no-up] [--down|--no-down] [--left|--no-left] [--right|--no-right] [--fire|--no-fire]", "Set joystick state"),
    ("joystick.press",     "[--port <1|2>] [--up] [--down] [--left] [--right] [--fire]", "Press and hold joystick actions"),
    ("joystick.release",   "[--port <1|2>] [--up] [--down] [--left] [--right] [--fire]", "Release held joystick actions"),
    ("joystick.releaseall","--port <1|2>",                   "Release all held joystick actions on one port"),
    ("keyboard.press",     "--key <keyname>",              "Press (hold) a named key"),
    ("keyboard.release",   "--key <keyname>",              "Release a previously pressed key"),
    ("keyboard.releaseall","",                             "Release all injected keys"),
    ("keyboard.iskeydown", "--key <keyname>",              "Check if a key is currently down"),
    ("keyboard.getall",    "",                             "List all valid key names for the current system"),
    ("c64.type",           "--text <string>",                         "Paste text into C64 keyboard buffer (C64 only)"),
    ("c64.loadprg",        "--file <path.prg> | --data <base64>",    "Load a PRG file into C64 memory (C64 only)"),
    ("c64.isbasicstarted", "",                                        "Check if C64 BASIC has finished initializing (C64 only)"),
    ("c64.getbasicsource", "",                                        "Get the current BASIC program as text (C64 only)"),
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

var cmdArgs = args[cmdIndex..];
var buildResult = RemoteClientRequestBuilder.Build(cmdArgs);
if (buildResult.Error != null)
{
    Console.Error.WriteLine(buildResult.Error);
    return 2;
}

var requestObj = buildResult.Request!;
var outputFile = buildResult.OutputFile;

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
    Console.WriteLine("  dotnet-6502-remote emu.systems");
    Console.WriteLine("  dotnet-6502-remote emu.selectsystem --name C64");
    Console.WriteLine("  dotnet-6502-remote emu.variants");
    Console.WriteLine("  dotnet-6502-remote emu.selectvariant --name \"C64 - Default\"");
    Console.WriteLine("  dotnet-6502-remote cpu.get");
    Console.WriteLine("  dotnet-6502-remote cpu.set --pc C000 --a 0");
    Console.WriteLine("  dotnet-6502-remote cpu.set --flags \"----I---\"");
    Console.WriteLine("  dotnet-6502-remote mem.read --addr C000 --len 16");
    Console.WriteLine("  dotnet-6502-remote mem.write --addr C000 --data 169,0,133,208");
    Console.WriteLine("  dotnet-6502-remote mem.loadbin --addr 0801 --file /path/to/binary.bin");
    Console.WriteLine("  dotnet-6502-remote joystick.set --port 1 --up --fire");
    Console.WriteLine("  dotnet-6502-remote joystick.press --port 1 --up --fire");
    Console.WriteLine("  dotnet-6502-remote joystick.release --port 1 --up");
    Console.WriteLine("  dotnet-6502-remote joystick.releaseall --port 1");
    Console.WriteLine("  dotnet-6502-remote joystick.set --port 1 --no-up --fire false");
    Console.WriteLine("  dotnet-6502-remote keyboard.press --key space");
    Console.WriteLine("  dotnet-6502-remote keyboard.release --key space");
    Console.WriteLine("  dotnet-6502-remote keyboard.releaseall");
    Console.WriteLine("  dotnet-6502-remote keyboard.iskeydown --key return");
    Console.WriteLine("  dotnet-6502-remote keyboard.getall");
    Console.WriteLine("  dotnet-6502-remote c64.type --text \"LOAD\\\"*\\\",8,1\"");
    Console.WriteLine("  dotnet-6502-remote c64.loadprg --file /path/to/program.prg");
    Console.WriteLine("  dotnet-6502-remote c64.isbasicstarted");
    Console.WriteLine("  dotnet-6502-remote c64.getbasicsource");
    Console.WriteLine("  dotnet-6502-remote screenshot --output /tmp/screen.png");
    Console.WriteLine("  dotnet-6502-remote ui.message --text \"Hello from remote\" --level info");
}
