using System.Text.Json;

namespace Highbyte.DotNet6502.App.RemoteClient;

internal sealed class RemoteClientRequestBuildResult
{
    public Dictionary<string, object?>? Request { get; init; }
    public string? OutputFile { get; init; }
    public string? Error { get; init; }
}

internal static class RemoteClientRequestBuilder
{
    public static RemoteClientRequestBuildResult Build(string[] cmdArgs)
    {
        if (cmdArgs.Length == 0)
        {
            return new RemoteClientRequestBuildResult { Error = "No command specified. Use --help for usage." };
        }

        var cmd = cmdArgs[0];
        var parameters = ParseParams(cmdArgs[1..]);
        var request = new Dictionary<string, object?> { ["id"] = 1, ["cmd"] = cmd };
        string? outputFile = null;

        switch (cmd)
        {
            case "server.info":
            case "emu.state":
            case "emu.start":
            case "emu.stop":
            case "emu.pause":
            case "emu.reset":
            case "emu.quit":
            case "emu.systems":
            case "emu.storagepaths":
            case "emu.variants":
            case "cpu.get":
            case "keyboard.releaseall":
            case "keyboard.getall":
            case "c64.isbasicstarted":
            case "c64.getbasicsource":
                break;

            case "emu.selectsystem":
            case "emu.selectvariant":
                if (parameters.TryGetValue("name", out var sysName)) request["name"] = sysName;
                break;

            case "emu.savesnapshot":
            case "emu.loadsnapshot":
                if (!parameters.TryGetValue("path", out var snapshotPath) || string.IsNullOrEmpty(snapshotPath))
                    return new RemoteClientRequestBuildResult { Error = $"{cmd} requires --path <file.d6502snap>" };
                request["path"] = snapshotPath;
                break;

            case "emu.runframes":
                if (parameters.TryGetValue("count", out var frameCount))
                {
                    if (!int.TryParse(frameCount, out int countVal) || countVal < 1)
                        return new RemoteClientRequestBuildResult { Error = "emu.runframes --count must be an integer >= 1" };
                    request["count"] = countVal;
                }
                break;

            case "cpu.set":
                if (parameters.TryGetValue("pc", out var cpuPc)) request["pc"] = cpuPc;
                if (parameters.TryGetValue("a", out var cpuA) && int.TryParse(cpuA, out int aVal)) request["a"] = aVal;
                if (parameters.TryGetValue("x", out var cpuX) && int.TryParse(cpuX, out int xVal)) request["x"] = xVal;
                if (parameters.TryGetValue("y", out var cpuY) && int.TryParse(cpuY, out int yVal)) request["y"] = yVal;
                if (parameters.TryGetValue("sp", out var cpuSp) && int.TryParse(cpuSp, out int spVal)) request["sp"] = spVal;
                if (parameters.TryGetValue("flags", out var cpuFlags)) request["flags"] = cpuFlags;
                break;

            case "mem.read":
                if (parameters.TryGetValue("addr", out var addr)) request["addr"] = addr;
                if (parameters.TryGetValue("len", out var len) && int.TryParse(len, out int lenInt)) request["len"] = lenInt;
                break;

            case "mem.write":
                if (parameters.TryGetValue("addr", out var writeAddr)) request["addr"] = writeAddr;
                if (parameters.TryGetValue("data", out var data))
                    request["data"] = data!.Split(',').Select(b => int.Parse(b.Trim())).ToArray();
                break;

            case "mem.loadbin":
                if (parameters.TryGetValue("addr", out var binAddr)) request["addr"] = binAddr;
                if (parameters.TryGetValue("file", out var binFile))
                {
                    if (!File.Exists(binFile))
                        return new RemoteClientRequestBuildResult { Error = $"File not found: {binFile}" };
                    request["data"] = Convert.ToBase64String(File.ReadAllBytes(binFile!));
                }
                else if (parameters.TryGetValue("data", out var binData))
                {
                    request["data"] = binData;
                }
                else
                {
                    return new RemoteClientRequestBuildResult { Error = "mem.loadbin requires --file <path> or --data <base64>" };
                }
                break;

            case "joystick.set":
                if (parameters.TryGetValue("port", out var port) && int.TryParse(port, out int parsedPort))
                    request["port"] = parsedPort;

                var joystickParseError = ApplyJoystickState(parameters, request, allowExplicitFalse: true);
                if (joystickParseError != null)
                    return new RemoteClientRequestBuildResult { Error = joystickParseError };
                break;

            case "joystick.press":
            case "joystick.release":
                if (parameters.TryGetValue("port", out var holdPort) && int.TryParse(holdPort, out int parsedHoldPort))
                    request["port"] = parsedHoldPort;

                var holdParseError = ApplyJoystickState(parameters, request, allowExplicitFalse: false);
                if (holdParseError != null)
                    return new RemoteClientRequestBuildResult { Error = holdParseError };
                break;

            case "joystick.releaseall":
                if (parameters.TryGetValue("port", out var releaseAllPort) && int.TryParse(releaseAllPort, out int parsedReleaseAllPort))
                    request["port"] = parsedReleaseAllPort;
                else
                    return new RemoteClientRequestBuildResult { Error = "joystick.releaseall requires --port <1|2>." };
                break;

            case "keyboard.press":
            case "keyboard.release":
            case "keyboard.iskeydown":
                if (parameters.TryGetValue("key", out var key)) request["key"] = key;
                break;

            case "c64.type":
                if (parameters.TryGetValue("text", out var text)) request["text"] = text;
                break;

            case "c64.loadprg":
                if (parameters.TryGetValue("file", out var prgFile))
                {
                    if (!File.Exists(prgFile))
                        return new RemoteClientRequestBuildResult { Error = $"File not found: {prgFile}" };
                    request["data"] = Convert.ToBase64String(File.ReadAllBytes(prgFile!));
                }
                else if (parameters.TryGetValue("data", out var prgData))
                {
                    request["data"] = prgData;
                }
                else
                {
                    return new RemoteClientRequestBuildResult { Error = "c64.loadprg requires --file <path.prg> or --data <base64>" };
                }
                break;

            case "screenshot":
                if (parameters.TryGetValue("output", out var outFile)) outputFile = outFile;
                break;

            case "ui.message":
                if (parameters.TryGetValue("text", out var message)) request["text"] = message;
                if (parameters.TryGetValue("level", out var level)) request["level"] = level;
                break;

            default:
                return new RemoteClientRequestBuildResult { Error = $"Unknown command: {cmd}. Use --help for usage." };
        }

        return new RemoteClientRequestBuildResult
        {
            Request = request,
            OutputFile = outputFile,
        };
    }

    private static string? ApplyJoystickState(Dictionary<string, string?> parameters, Dictionary<string, object?> request, bool allowExplicitFalse)
    {
        foreach (var action in new[] { "up", "down", "left", "right", "fire" })
        {
            var parseResult = ParseJoystickState(parameters, action, allowExplicitFalse);
            if (parseResult.Error != null)
                return parseResult.Error;
            if (parseResult.Value.HasValue)
                request[action] = parseResult.Value.Value;
        }

        return null;
    }

    private static (bool? Value, string? Error) ParseJoystickState(Dictionary<string, string?> parameters, string action, bool allowExplicitFalse)
    {
        var releaseAlias = $"no-{action}";
        var hasPositive = parameters.TryGetValue(action, out var rawValue);
        var hasNegative = parameters.ContainsKey(releaseAlias);

        if (!allowExplicitFalse)
        {
            if (hasNegative)
                return (null, $"--{releaseAlias} is only supported for joystick.set.");

            if (!hasPositive)
                return (null, null);

            if (rawValue == null)
                return (true, null);

            if (bool.TryParse(rawValue, out var parsedLatchedBool))
            {
                return parsedLatchedBool
                    ? (true, null)
                    : (null, $"Only joystick.set accepts explicit false values for --{action}.");
            }

            return (null, $"Invalid boolean value for --{action}: {rawValue}. Use true or false.");
        }

        if (hasPositive && hasNegative)
        {
            return (null, $"Conflicting options for joystick action '{action}': use either --{action} or --{releaseAlias}, not both.");
        }

        if (hasNegative)
            return (false, null);

        if (!hasPositive)
            return (null, null);

        if (rawValue == null)
            return (true, null);

        if (bool.TryParse(rawValue, out var parsedBool))
            return (parsedBool, null);

        return (null, $"Invalid boolean value for --{action}: {rawValue}. Use true or false.");
    }

    private static Dictionary<string, string?> ParseParams(string[] args)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--"))
                continue;

            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                result[key] = args[i + 1];
                i++;
            }
            else
            {
                result[key] = null;
            }
        }
        return result;
    }
}
