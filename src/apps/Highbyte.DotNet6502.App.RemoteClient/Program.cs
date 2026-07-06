using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Highbyte.DotNet6502.App.RemoteClient;
using Highbyte.DotNet6502.Updates;

// Remote client for the DotNet 6502 emulator TCP remote control server.
// Usage: dotnet-6502-remote [--port <port>] [--host <host>] <command> [params...]
// Usage: dotnet-6502-remote --help

const int DefaultPort = 6510;
const string DefaultHost = "127.0.0.1";

// Command definitions for --help output and self-description
var commands = new[]
{
    ("server.info",      "",                              "Get the server host app name and release-stamped version"),
    ("emu.state",        "",                              "Get emulator state, system, and variant"),
    ("emu.start",        "",                              "Start (or resume from paused) the emulator"),
    ("emu.stop",         "",                              "Stop the emulator"),
    ("emu.pause",        "",                              "Pause the emulator"),
    ("emu.reset",        "",                              "Reset the emulator"),
    ("emu.runframes",    "[--count <n>]",                 "Step the emulator deterministically by n frames (default 1) and render (for screenshots); requires a paused/stopped emulator (rejected while running)"),
    ("emu.quit",         "",                              "Quit the emulator (headless only)"),
    ("emu.systems",      "",                              "List available system names"),
    ("emu.storagepaths", "",                              "Show storage paths used by the running emulator host"),
    ("emu.selectsystem", "--name <system>",               "Select a system (emulator must be stopped)"),
    ("emu.variants",     "",                              "List config variants for the current system"),
    ("emu.selectvariant","--name <variant>",              "Select a config variant (emulator must be stopped)"),
    ("emu.savesnapshot", "--path <file.d6502snap>",       "Save current emulator state to a snapshot file (relative paths use the emulator host's snapshot directory)"),
    ("emu.loadsnapshot", "--path <file.d6502snap>",       "Restore emulator state from a snapshot file (relative paths use the emulator host's snapshot directory; leaves it paused)"),
    ("cpu.get",          "",                              "Get CPU registers"),
    ("cpu.set",          "[--pc <hex>] [--a <0-255>] [--x <0-255>] [--y <0-255>] [--sp <0-255>] [--flags <NVUBDIZC>]", "Set CPU registers"),
    ("mem.read",         "--addr <hex> --len <int>",                          "Read bytes from memory"),
    ("mem.write",        "--addr <hex> --data <b,b,..>",                       "Write bytes to memory"),
    ("mem.loadbin",      "--addr <hex> --file <path> | --data <base64>",       "Load binary file into memory at a specific address"),
    ("joystick.set",       "--port <1|2> [--up|--no-up] [--down|--no-down] [--left|--no-left] [--right|--no-right] [--fire|--no-fire]", "Set joystick state"),
    ("joystick.press",     "[--port <1|2>] [--up] [--down] [--left] [--right] [--fire]", "Press and hold joystick actions"),
    ("joystick.release",   "[--port <1|2>] [--up] [--down] [--left] [--right] [--fire]", "Release held joystick actions"),
    ("joystick.releaseall","--port <1|2>",                   "Release all held joystick actions on one port"),
    ("keyboard.press",     "--key <keyname>",              "Press (hold) a named key (use keyboard.getall to list valid names)"),
    ("keyboard.release",   "--key <keyname>",              "Release a previously pressed key"),
    ("keyboard.releaseall","",                             "Release all injected keys"),
    ("keyboard.iskeydown", "--key <keyname>",              "Check if a key is currently down"),
    ("keyboard.getall",    "",                             "List all valid key names for the current system"),
    ("c64.type",           "--text <string>",                         "Paste text into C64 keyboard buffer (C64 only; use lowercase letters — see NOTES)"),
    ("c64.loadprg",        "--file <path.prg> | --data <base64>",    "Load a PRG file into C64 memory (C64 only)"),
    ("c64.isbasicstarted", "",                                        "Check if C64 BASIC has finished initializing (C64 only)"),
    ("c64.getbasicsource", "",                                        "Get the current BASIC program as text (C64 only)"),
    ("screenshot",    "[--output <file.png>]",      "Capture screenshot (Base64 PNG or saved to file)"),
    ("ui.message",    "--text <string> [--level info|warning|error]", "Display message in emulator UI"),
};

// Update check: explicit flags (--version / --check-update / --update) only, to stdout. RemoteClient
// is a request/response automation tool whose stdout is the server response, so it does NO automatic
// startup check/notice and adds no logging that could interfere with scripted consumers.
if (ConsoleUpdateCli.WantsHandling(args))
    return await ConsoleUpdateCli.RunAsync(
        args,
        new AppUpdateDescriptor { HomebrewPackage = "dotnet-6502-remote", ScoopPackage = "dotnet-6502-remote" },
        Console.Out);

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

// Preflight version check: connect, ask the server for its app version, and warn if it differs from
// this client's version. Kept separate from the normal command path because it does its own compare.
if (cmdArgs[0] == "--check-server-version")
    return await CheckServerVersionAsync(host, port);

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

async Task<int> CheckServerVersionAsync(string serverHost, int serverPort)
{
    try
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(serverHost, serverPort);
        using var stream = tcp.GetStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync("{\"id\":1,\"cmd\":\"server.info\"}");

        var line = await reader.ReadLineAsync();
        if (line == null)
        {
            Console.Error.WriteLine("No response from server.");
            return 1;
        }

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
        if (!ok)
        {
            var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;
            // A server that predates this feature rejects the unknown command → it's older than the client.
            if (error != null && error.Contains("Unknown command", StringComparison.OrdinalIgnoreCase))
                return Emit(ServerVersionCheck.ServerTooOld(null));

            Console.Error.WriteLine($"Error: {error ?? "Unknown error"}");
            return 1;
        }

        var app = root.TryGetProperty("app", out var appProp) ? appProp.GetString() : null;
        var appVersionRaw = root.TryGetProperty("appversion", out var verProp) ? verProp.GetString() : null;

        return Emit(ServerVersionCheck.Evaluate(app, AppVersion.Parse(appVersionRaw), AppVersion.GetCurrent()));
    }
    catch (SocketException ex)
    {
        Console.Error.WriteLine($"Could not connect to {serverHost}:{serverPort} — {ex.Message}");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }

    static int Emit(ServerVersionCheck.Result result)
    {
        foreach (var l in result.StdoutLines) Console.WriteLine(l);
        foreach (var l in result.StderrLines) Console.Error.WriteLine(l);
        return result.ExitCode;
    }
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
    Console.WriteLine("  --version       Print the client version and exit");
    Console.WriteLine("  --check-update  Check for a newer release of dotnet-6502-remote");
    Console.WriteLine("  --update        Update dotnet-6502-remote via its package manager");
    Console.WriteLine("  --check-server-version");
    Console.WriteLine("                  Connect and warn if the server app version differs from this client");
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
    Console.WriteLine("  dotnet-6502-remote emu.storagepaths");
    Console.WriteLine("  dotnet-6502-remote emu.selectsystem --name C64");
    Console.WriteLine("  dotnet-6502-remote emu.variants");
    Console.WriteLine("  dotnet-6502-remote emu.selectvariant --name \"C64 - Default\"");
    Console.WriteLine("  dotnet-6502-remote emu.savesnapshot --path state.d6502snap");
    Console.WriteLine("  dotnet-6502-remote emu.loadsnapshot --path state.d6502snap");
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
    Console.WriteLine("  dotnet-6502-remote c64.type --text \"load\\\"*\\\",8,1\"");
    Console.WriteLine("  dotnet-6502-remote c64.loadprg --file /path/to/program.prg");
    Console.WriteLine("  dotnet-6502-remote c64.isbasicstarted");
    Console.WriteLine("  dotnet-6502-remote c64.getbasicsource");
    Console.WriteLine("  dotnet-6502-remote screenshot --output /tmp/screen.png");
    Console.WriteLine("  dotnet-6502-remote ui.message --text \"Hello from remote\" --level info");
    Console.WriteLine();
    Console.WriteLine("NOTES:");
    Console.WriteLine("  * c64.type case mapping: The C64 default mode displays lowercase input 'a'-'z'");
    Console.WriteLine("    as uppercase A-Z on screen. Uppercase input 'A'-'Z' produces graphics chars.");
    Console.WriteLine("    Use lowercase text for BASIC keywords: \"sys 49152\" displays as SYS 49152.");
    Console.WriteLine();
    Console.WriteLine("  * keyboard.getall: Call this first to discover valid key names for the current");
    Console.WriteLine("    system (e.g. space, return, a-z, f1, f3, f5, f7, crsrdown, crsrright, stop).");
    Console.WriteLine();
    Console.WriteLine("  * cpu.set --pc <addr>: The simplest way to start machine code on any system.");
    Console.WriteLine("    Write code with mem.write, then set the PC — no BASIC or keyboard needed.");
    Console.WriteLine("    Takes effect at the next frame boundary. Best for programs that loop");
    Console.WriteLine("    forever or never return. For routines ending with RTS, use c64.type");
    Console.WriteLine("    with \"sys <addr>\" instead so BASIC sets up the proper return address.");
    Console.WriteLine();
    Console.WriteLine("  * Frame-boundary commands (mem.write, keyboard.press/release, c64.type,");
    Console.WriteLine("    c64.loadprg, joystick.*) require the emulator to be Running. Use emu.state");
    Console.WriteLine("    to confirm before sending them.");
}
