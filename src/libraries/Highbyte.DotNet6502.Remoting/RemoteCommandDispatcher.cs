using System.Reflection;
using System.Text.Json;
using Highbyte.DotNet6502.Remoting.Protocol;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Remoting;

/// <summary>
/// Routes incoming <see cref="RemoteCommand"/>s to the correct handler and dispatch path:
/// <list type="bullet">
///   <item>Read-only queries — executed directly on the session thread.</item>
///   <item>Control operations — dispatched via <see cref="IRemoteControlEnvironment.RunOnUiThread"/>.</item>
///   <item>Frame-boundary operations — enqueued via <see cref="IRemotableHostApp.EnqueueRemoteAction"/>.</item>
/// </list>
/// </summary>
public class RemoteCommandDispatcher
{
    private readonly IRemoteControlEnvironment _environment;
    private readonly ILogger<RemoteCommandDispatcher>? _logger;

    public RemoteCommandDispatcher(IRemoteControlEnvironment environment, ILoggerFactory? loggerFactory = null)
    {
        _environment = environment;
        _logger = loggerFactory?.CreateLogger<RemoteCommandDispatcher>();
    }

    public async Task<RemoteCommandResult> DispatchAsync(RemoteCommand cmd)
    {
        _logger?.LogDebug("[Dispatcher] cmd={Cmd} id={Id}", cmd.Cmd, cmd.Id);
        try
        {
            return cmd.Cmd switch
            {
                "emu.state"    => HandleEmuState(cmd.Id),
                "server.info"  => HandleServerInfo(cmd.Id),
                "emu.start"    => await HandleUiAsync(cmd.Id, async hostApp => await hostApp.Start()),
                "emu.stop"     => await HandleUiAsync(cmd.Id, hostApp => { hostApp.Stop();  return Task.CompletedTask; }),
                "emu.pause"    => await HandleUiAsync(cmd.Id, hostApp => { hostApp.Pause(); return Task.CompletedTask; }),
                "emu.reset"    => await HandleUiAsync(cmd.Id, async hostApp => await hostApp.Reset()),
                "emu.quit"          => HandleEmuQuit(cmd.Id),
                "emu.systems"       => HandleEmuSystems(cmd.Id),
                "emu.storagepaths"  => await HandleEmuStoragePaths(cmd.Id),
                "emu.selectsystem"  => string.IsNullOrEmpty(cmd.Name)
                    ? Err(cmd.Id, "Missing 'name' parameter")
                    : await HandleUiAsync(cmd.Id, async hostApp => await hostApp.SelectSystem(cmd.Name)),
                "emu.variants"      => HandleEmuVariants(cmd.Id),
                "emu.selectvariant" => string.IsNullOrEmpty(cmd.Name)
                    ? Err(cmd.Id, "Missing 'name' parameter")
                    : await HandleUiAsync(cmd.Id, async hostApp => await hostApp.SelectSystemConfigurationVariant(cmd.Name)),
                "emu.savesnapshot" => string.IsNullOrEmpty(cmd.Path)
                    ? Err(cmd.Id, "Missing 'path' parameter")
                    : await HandleUiAsync(cmd.Id, hostApp => SaveSnapshotToFile(hostApp, cmd.Path!)),
                "emu.loadsnapshot" => string.IsNullOrEmpty(cmd.Path)
                    ? Err(cmd.Id, "Missing 'path' parameter")
                    : await HandleUiAsync(cmd.Id, hostApp => LoadSnapshotFromFile(hostApp, cmd.Path!)),
                "emu.runframes" => await HandleUiAsync(cmd.Id, hostApp => hostApp.StepEmulatorFramesAsync(cmd.Count ?? 1)),
                "cpu.get"      => HandleCpuGet(cmd.Id),
                "cpu.set"      => await HandleFrameAsync(cmd.Id, hostApp => CpuSetDirect(hostApp, cmd)),
                "mem.read"     => HandleMemRead(cmd.Id, cmd),
                "mem.write"    => await HandleFrameAsync(cmd.Id, hostApp => MemWriteDirect(hostApp, cmd)),
                "mem.loadbin"  => await HandleFrameAsync(cmd.Id, hostApp => MemLoadBinDirect(hostApp, cmd)),
                "c64.loadprg"        => await HandleFrameAsync(cmd.Id, hostApp => C64LoadPrgDirect(hostApp, cmd)),
                "joystick.set"       => await HandleFrameAsync(cmd.Id, hostApp => JoystickSetDirect(hostApp, cmd)),
                "joystick.press"     => await HandleFrameAsync(cmd.Id, hostApp => JoystickPressDirect(hostApp, cmd)),
                "joystick.release"   => await HandleFrameAsync(cmd.Id, hostApp => JoystickReleaseDirect(hostApp, cmd)),
                "joystick.releaseall"=> await HandleFrameAsync(cmd.Id, hostApp => JoystickReleaseAllDirect(hostApp, cmd)),
                "keyboard.press"     => await HandleFrameAsync(cmd.Id, hostApp => KeyboardPressDirect(hostApp, cmd)),
                "keyboard.release"   => await HandleFrameAsync(cmd.Id, hostApp => KeyboardReleaseDirect(hostApp, cmd)),
                "keyboard.releaseall"=> await HandleFrameAsync(cmd.Id, hostApp => KeyboardReleaseAllDirect(hostApp)),
                "keyboard.iskeydown" => HandleKeyIsDown(cmd.Id, cmd),
                "keyboard.getall"    => HandleKeyGetAll(cmd.Id),
                "c64.type"           => await HandleC64TypeAsync(cmd.Id, cmd),
                "c64.isbasicstarted" => HandleC64IsBasicStarted(cmd.Id),
                "c64.getbasicsource" => HandleC64GetBasicSource(cmd.Id),
                "screenshot"   => HandleScreenshot(cmd.Id),
                "ui.message"   => HandleUiMessage(cmd.Id, cmd),
                _              => Err(cmd.Id, $"Unknown command: {cmd.Cmd}"),
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("[Dispatcher] Unhandled exception for cmd={Cmd}: {Message}", cmd.Cmd, ex.Message);
            return Err(cmd.Id, ex.Message);
        }
    }

    // --- Direct read handlers ---

    /// <summary>
    /// Cheap, read-only server-info query used by the remote client's <c>--check-server-version</c>
    /// preflight. Returns the host app name and the raw release-stamped app version (the entry
    /// assembly's <see cref="AssemblyInformationalVersionAttribute"/>); the client normalizes and
    /// compares it against its own version. Works regardless of emulator state.
    /// </summary>
    private RemoteCommandResult HandleServerInfo(int? id)
    {
        var informationalVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return new RemoteCommandResult
        {
            Id = id, Ok = true,
            App = _environment.GetHostApp()?.HostName,
            AppVersion = informationalVersion,
        };
    }

    private RemoteCommandResult HandleEmuState(int? id)
    {
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        return new RemoteCommandResult
        {
            Id = id, Ok = true,
            State = hostApp.EmulatorState.ToString(),
            System = hostApp.SelectedSystemName,
            Variant = hostApp.SelectedSystemConfigurationVariant,
        };
    }

    private RemoteCommandResult HandleCpuGet(int? id)
    {
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        var sys = hostApp.CurrentRunningSystem;
        if (sys == null) return Err(id, "Emulator not started");
        var cpu = sys.CPU;
        var ps = cpu.ProcessorStatus;
        return new RemoteCommandResult
        {
            Id = id, Ok = true,
            PC = $"{cpu.PC:X4}",
            A  = cpu.A,
            X  = cpu.X,
            Y  = cpu.Y,
            SP = cpu.SP,
            Flags = $"{(ps.Negative?'N':'-')}{(ps.Overflow?'V':'-')}{(ps.Unused?'U':'-')}{(ps.Break?'B':'-')}{(ps.Decimal?'D':'-')}{(ps.InterruptDisable?'I':'-')}{(ps.Zero?'Z':'-')}{(ps.Carry?'C':'-')}",
        };
    }

    private RemoteCommandResult HandleMemRead(int? id, RemoteCommand cmd)
    {
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        var sys = hostApp.CurrentRunningSystem;
        if (sys == null) return Err(id, "Emulator not started");

        if (string.IsNullOrWhiteSpace(cmd.Addr))
            return Err(id, "Missing 'addr' parameter");
        if (!ushort.TryParse(cmd.Addr, System.Globalization.NumberStyles.HexNumber, null, out var addr))
            return Err(id, $"Invalid hex address: {cmd.Addr}");

        int len = cmd.Len ?? 1;
        if (len < 1 || len > 4096) return Err(id, "len must be between 1 and 4096");

        var bytes = new int[len];
        for (int i = 0; i < len; i++)
            bytes[i] = sys.Mem[(ushort)((addr + i) & 0xFFFF)];

        return new RemoteCommandResult { Id = id, Ok = true, Data = bytes };
    }

    private RemoteCommandResult HandleScreenshot(int? id)
    {
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        var png = hostApp.CaptureScreenshotPng();
        if (png == null) return Err(id, "Screenshot not available");
        return new RemoteCommandResult
        {
            Id = id, Ok = true,
            Format = "png",
            Data = Convert.ToBase64String(png),
        };
    }

    private RemoteCommandResult HandleUiMessage(int? id, RemoteCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.Text)) return Err(id, "Missing 'text' parameter");
        _environment.DisplayRemoteMessage(cmd.Text, cmd.Level ?? "info");
        return Ok(id);
    }

    private RemoteCommandResult HandleEmuSystems(int? id)
    {
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        return new RemoteCommandResult { Id = id, Ok = true, Data = hostApp.AvailableSystemNames.Order().ToArray() };
    }

    private RemoteCommandResult HandleEmuVariants(int? id)
    {
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        return new RemoteCommandResult { Id = id, Ok = true, Data = hostApp.AllSelectedSystemConfigurationVariants.ToArray() };
    }

    private async Task<RemoteCommandResult> HandleEmuStoragePaths(int? id)
    {
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        var paths = await hostApp.GetStoragePathsInfoAsync().ConfigureAwait(false);
        return new RemoteCommandResult { Id = id, Ok = true, Data = paths };
    }

    private RemoteCommandResult HandleEmuQuit(int? id)
    {
        if (!_environment.SupportsQuit)
            return Err(id, "emu.quit is not supported in this environment");
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        _environment.RunOnUiThread(() => hostApp.QuitApplication());
        return Ok(id);
    }

    // --- UI-thread dispatch ---

    private async Task<RemoteCommandResult> HandleUiAsync(int? id, Func<IRemotableHostApp, Task> action)
    {
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _environment.RunOnUiThread(async () =>
        {
            try
            {
                await action(hostApp);
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        await tcs.Task;
        return Ok(id);
    }

    // --- Frame-boundary dispatch ---

    private async Task<RemoteCommandResult> HandleFrameAsync(int? id, Action<IRemotableHostApp> action)
    {
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        if (hostApp.EmulatorState != EmulatorState.Running) return Err(id, $"Emulator is not running (state: {hostApp.EmulatorState})");

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        hostApp.EnqueueRemoteAction(() =>
        {
            try
            {
                action(hostApp);
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        await tcs.Task;
        return Ok(id);
    }

    // --- Frame-boundary action implementations ---

    private static void MemWriteDirect(IRemotableHostApp hostApp, RemoteCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Addr))
            throw new ArgumentException("Missing 'addr'");
        if (!ushort.TryParse(cmd.Addr, System.Globalization.NumberStyles.HexNumber, null, out var addr))
            throw new ArgumentException($"Invalid hex address: {cmd.Addr}");
        if (cmd.Data == null || cmd.Data.Value.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("'data' must be a JSON array of byte values");

        var sys = hostApp.CurrentRunningSystem ?? throw new InvalidOperationException("Emulator not running");
        int i = 0;
        foreach (var elem in cmd.Data.Value.EnumerateArray())
            sys.Mem[(ushort)((addr + i++) & 0xFFFF)] = (byte)elem.GetInt32();
    }

    private static void CpuSetDirect(IRemotableHostApp hostApp, RemoteCommand cmd)
    {
        if (cmd.PC == null && !cmd.A.HasValue && !cmd.X.HasValue && !cmd.Y.HasValue && !cmd.SP.HasValue && cmd.Flags == null)
            throw new ArgumentException("No registers specified; provide at least one of: pc, a, x, y, sp, flags");

        var sys = hostApp.CurrentRunningSystem ?? throw new InvalidOperationException("Emulator not running");
        var cpu = sys.CPU;

        if (cmd.PC != null)
        {
            if (!ushort.TryParse(cmd.PC, System.Globalization.NumberStyles.HexNumber, null, out var pc))
                throw new ArgumentException($"Invalid hex address for 'pc': {cmd.PC}");
            cpu.PC = pc;
        }
        if (cmd.A.HasValue)  { if (cmd.A.Value  is < 0 or > 255) throw new ArgumentException("'a' must be 0–255");  cpu.A  = (byte)cmd.A.Value;  }
        if (cmd.X.HasValue)  { if (cmd.X.Value  is < 0 or > 255) throw new ArgumentException("'x' must be 0–255");  cpu.X  = (byte)cmd.X.Value;  }
        if (cmd.Y.HasValue)  { if (cmd.Y.Value  is < 0 or > 255) throw new ArgumentException("'y' must be 0–255");  cpu.Y  = (byte)cmd.Y.Value;  }
        if (cmd.SP.HasValue) { if (cmd.SP.Value is < 0 or > 255) throw new ArgumentException("'sp' must be 0–255"); cpu.SP = (byte)cmd.SP.Value; }
        if (cmd.Flags != null) cpu.ProcessorStatus.Value = ParseFlagsString(cmd.Flags);
    }

    private static byte ParseFlagsString(string flags)
    {
        if (flags.Length != 8)
            throw new ArgumentException($"'flags' must be exactly 8 characters (NVUBDIZC format, e.g. \"----I---\"), got: \"{flags}\"");
        byte value = 0;
        ReadOnlySpan<char> flagLetters = ['N', 'V', 'U', 'B', 'D', 'I', 'Z', 'C'];
        for (int i = 0; i < 8; i++)
        {
            if (char.ToUpperInvariant(flags[i]) == flagLetters[i])
                value |= (byte)(1 << (7 - i));
        }
        return value;
    }

    private static void MemLoadBinDirect(IRemotableHostApp hostApp, RemoteCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Addr))
            throw new ArgumentException("Missing 'addr' parameter");
        if (!ushort.TryParse(cmd.Addr, System.Globalization.NumberStyles.HexNumber, null, out var addr))
            throw new ArgumentException($"Invalid hex address: {cmd.Addr}");
        if (cmd.Data == null || cmd.Data.Value.ValueKind != System.Text.Json.JsonValueKind.String)
            throw new ArgumentException("'data' must be a base64-encoded string");

        byte[] bytes;
        try { bytes = Convert.FromBase64String(cmd.Data.Value.GetString()!); }
        catch { throw new ArgumentException("'data' is not valid base64"); }

        var sys = hostApp.CurrentRunningSystem ?? throw new InvalidOperationException("Emulator not running");
        for (int i = 0; i < bytes.Length; i++)
            sys.Mem[(ushort)((addr + i) & 0xFFFF)] = bytes[i];
    }

    private static void C64LoadPrgDirect(IRemotableHostApp hostApp, RemoteCommand cmd)
    {
        if (cmd.Data == null || cmd.Data.Value.ValueKind != System.Text.Json.JsonValueKind.String)
            throw new ArgumentException("'data' must be a base64-encoded string containing the PRG file bytes");

        byte[] prgBytes;
        try { prgBytes = Convert.FromBase64String(cmd.Data.Value.GetString()!); }
        catch { throw new ArgumentException("'data' is not valid base64"); }

        if (prgBytes.Length < 3)
            throw new ArgumentException("PRG data too short: minimum 3 bytes (2-byte load address + at least 1 data byte)");

        var sys = hostApp.CurrentRunningSystem ?? throw new InvalidOperationException("Emulator not running");
        if (sys is not C64 c64) throw new InvalidOperationException("Current system is not a C64");

        ushort loadAddr = (ushort)(prgBytes[0] | (prgBytes[1] << 8));
        for (int i = 2; i < prgBytes.Length; i++)
            c64.Mem[(ushort)((loadAddr + i - 2) & 0xFFFF)] = prgBytes[i];
    }

    private static void JoystickSetDirect(IRemotableHostApp hostApp, RemoteCommand cmd)
    {
        var input = GetInputProvider(hostApp);
        int port = cmd.Port ?? 1;

        SetJoystickBool(input, port, "Up",    cmd.Up);
        SetJoystickBool(input, port, "Down",  cmd.Down);
        SetJoystickBool(input, port, "Left",  cmd.Left);
        SetJoystickBool(input, port, "Right", cmd.Right);
        SetJoystickBool(input, port, "Fire",  cmd.Fire);
    }

    private static void JoystickPressDirect(IRemotableHostApp hostApp, RemoteCommand cmd)
    {
        var input = GetInputProvider(hostApp);
        int port = cmd.Port ?? 1;

        SetHeldJoystickBool(input, port, "Up", cmd.Up, pressed: true);
        SetHeldJoystickBool(input, port, "Down", cmd.Down, pressed: true);
        SetHeldJoystickBool(input, port, "Left", cmd.Left, pressed: true);
        SetHeldJoystickBool(input, port, "Right", cmd.Right, pressed: true);
        SetHeldJoystickBool(input, port, "Fire", cmd.Fire, pressed: true);
    }

    private static void JoystickReleaseDirect(IRemotableHostApp hostApp, RemoteCommand cmd)
    {
        var input = GetInputProvider(hostApp);
        int port = cmd.Port ?? 1;

        SetHeldJoystickBool(input, port, "Up", cmd.Up, pressed: false);
        SetHeldJoystickBool(input, port, "Down", cmd.Down, pressed: false);
        SetHeldJoystickBool(input, port, "Left", cmd.Left, pressed: false);
        SetHeldJoystickBool(input, port, "Right", cmd.Right, pressed: false);
        SetHeldJoystickBool(input, port, "Fire", cmd.Fire, pressed: false);
    }

    private static void JoystickReleaseAllDirect(IRemotableHostApp hostApp, RemoteCommand cmd)
    {
        var input = GetInputProvider(hostApp);
        if (!cmd.Port.HasValue)
            throw new ArgumentException("Missing 'port' parameter");
        input.ReleaseAllHeldJoystickActions(cmd.Port.Value);
    }

    private static void SetJoystickBool(IInputInjector input, int port, string action, bool? value)
    {
        if (value.HasValue)
            input.SetJoystickAction(port, action, value.Value);
    }

    private static void SetHeldJoystickBool(IInputInjector input, int port, string action, bool? value, bool pressed)
    {
        if (value != true)
            return;

        if (pressed)
            input.HoldJoystickAction(port, action);
        else
            input.ReleaseHeldJoystickAction(port, action);
    }

    private static void KeyboardPressDirect(IRemotableHostApp hostApp, RemoteCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.Key)) throw new ArgumentException("Missing 'key' parameter");
        var input = GetInputProvider(hostApp);
        input.HoldKey(cmd.Key);
    }

    private static void KeyboardReleaseDirect(IRemotableHostApp hostApp, RemoteCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.Key)) throw new ArgumentException("Missing 'key' parameter");
        var input = GetInputProvider(hostApp);
        input.ReleaseHeldKey(cmd.Key);
    }

    private static void KeyboardReleaseAllDirect(IRemotableHostApp hostApp)
    {
        GetInputProvider(hostApp).ReleaseAllHeldKeys();
    }

    private RemoteCommandResult HandleKeyIsDown(int? id, RemoteCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.Key)) return Err(id, "Missing 'key' parameter");
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        var sys = hostApp.CurrentRunningSystem;
        if (sys == null) return Err(id, "Emulator not started");
        var input = sys.InputInjector;
        if (input == null) return Err(id, "System has no input injector");
        return new RemoteCommandResult { Id = id, Ok = true, IsDown = input.IsKeyDown(cmd.Key) };
    }

    private RemoteCommandResult HandleKeyGetAll(int? id)
    {
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) return Err(id, "Emulator not initialized");
        var sys = hostApp.CurrentRunningSystem;
        if (sys == null) return Err(id, "Emulator not started");
        var input = sys.InputInjector;
        if (input == null) return Err(id, "System has no input injector");
        return new RemoteCommandResult { Id = id, Ok = true, Data = input.GetAvailableKeys() };
    }

    private static IInputInjector GetInputProvider(IRemotableHostApp hostApp)
    {
        var sys = hostApp.CurrentRunningSystem ?? throw new InvalidOperationException("Emulator not running");
        return sys.InputInjector ?? throw new InvalidOperationException("System has no input injector");
    }

    private async Task<RemoteCommandResult> HandleC64TypeAsync(int? id, RemoteCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.Text)) return Err(id, "Missing 'text' parameter");
        var c64 = GetC64System(id, out var err);
        if (c64 == null) return err!;
        return await HandleFrameAsync(id, _ => c64.TextPaste.Paste(cmd.Text));
    }

    private RemoteCommandResult HandleC64IsBasicStarted(int? id)
    {
        var c64 = GetC64System(id, out var err);
        if (c64 == null) return err!;
        return new RemoteCommandResult { Id = id, Ok = true, IsBasicStarted = c64.HasBasicStarted() };
    }

    private RemoteCommandResult HandleC64GetBasicSource(int? id)
    {
        var c64 = GetC64System(id, out var err);
        if (c64 == null) return err!;
        var source = c64.HasBasicStarted() ? c64.BasicTokenParser.GetBasicText() : string.Empty;
        return new RemoteCommandResult { Id = id, Ok = true, Data = source };
    }

    private C64? GetC64System(int? id, out RemoteCommandResult? error)
    {
        error = null;
        var hostApp = _environment.GetHostApp();
        if (hostApp == null) { error = Err(id, "Emulator not initialized"); return null; }
        var sys = hostApp.CurrentRunningSystem;
        if (sys == null) { error = Err(id, "Emulator not started"); return null; }
        if (sys is not C64 c64) { error = Err(id, "Current system is not a C64"); return null; }
        return c64;
    }

    // --- Helpers ---

    // Snapshot save/load run on the UI thread (via HandleUiAsync) and read/write the .d6502snap on
    // the machine the emulator runs on. Exceptions (no provider, missing file, ...) propagate to
    // DispatchAsync's catch and are returned to the client as an error result.
    private static async Task SaveSnapshotToFile(IRemotableHostApp hostApp, string path)
    {
        if (!hostApp.CanSnapshotCurrentSystem)
            throw new InvalidOperationException(
                $"System '{hostApp.SelectedSystemName}' does not support snapshots (none selected/running?).");
        var snapshotPath = AppStoragePaths.ResolveSnapshotFilePath(path);
        var directory = System.IO.Path.GetDirectoryName(snapshotPath);
        if (!string.IsNullOrEmpty(directory))
            System.IO.Directory.CreateDirectory(directory);
        await using var fileStream = System.IO.File.Create(snapshotPath);
        await hostApp.SaveSnapshotAsync(fileStream);
    }

    private static async Task LoadSnapshotFromFile(IRemotableHostApp hostApp, string path)
    {
        var snapshotPath = AppStoragePaths.ResolveSnapshotFilePath(path);
        if (!System.IO.File.Exists(snapshotPath))
            throw new System.IO.FileNotFoundException($"Snapshot file not found: {snapshotPath}");
        await using var fileStream = System.IO.File.OpenRead(snapshotPath);
        await hostApp.LoadSnapshotAsync(fileStream);
    }

    private static RemoteCommandResult Ok(int? id) =>
        new() { Id = id, Ok = true };

    private static RemoteCommandResult Err(int? id, string error) =>
        new() { Id = id, Ok = false, Error = error };
}
