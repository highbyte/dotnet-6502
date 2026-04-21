using System.Text.Json;
using Highbyte.DotNet6502.Remoting.Protocol;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
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
                "emu.start"    => await HandleUiAsync(cmd.Id, async h => await h.Start()),
                "emu.stop"     => await HandleUiAsync(cmd.Id, h => { h.Stop();  return Task.CompletedTask; }),
                "emu.pause"    => await HandleUiAsync(cmd.Id, h => { h.Pause(); return Task.CompletedTask; }),
                "emu.reset"    => await HandleUiAsync(cmd.Id, async h => await h.Reset()),
                "emu.quit"     => HandleEmuQuit(cmd.Id),
                "cpu.get"      => HandleCpuGet(cmd.Id),
                "mem.read"     => HandleMemRead(cmd.Id, cmd),
                "mem.write"    => await HandleFrameAsync(cmd.Id, h => MemWriteDirect(h, cmd)),
                "joystick.set"       => await HandleFrameAsync(cmd.Id, h => JoystickSetDirect(h, cmd)),
                "joystick.press"     => await HandleFrameAsync(cmd.Id, h => JoystickPressDirect(h, cmd)),
                "joystick.release"   => await HandleFrameAsync(cmd.Id, h => JoystickReleaseDirect(h, cmd)),
                "joystick.releaseall"=> await HandleFrameAsync(cmd.Id, h => JoystickReleaseAllDirect(h, cmd)),
                "keyboard.press"     => await HandleFrameAsync(cmd.Id, h => KeyboardPressDirect(h, cmd)),
                "keyboard.release"   => await HandleFrameAsync(cmd.Id, h => KeyboardReleaseDirect(h, cmd)),
                "keyboard.releaseall"=> await HandleFrameAsync(cmd.Id, h => KeyboardReleaseAllDirect(h)),
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

    private RemoteCommandResult HandleEmuState(int? id)
    {
        var h = _environment.GetHostApp();
        if (h == null) return Err(id, "Emulator not initialized");
        return new RemoteCommandResult
        {
            Id = id, Ok = true,
            State = h.EmulatorState.ToString(),
            System = h.SelectedSystemName,
        };
    }

    private RemoteCommandResult HandleCpuGet(int? id)
    {
        var h = _environment.GetHostApp();
        if (h == null) return Err(id, "Emulator not initialized");
        var sys = h.CurrentRunningSystem;
        if (sys == null) return Err(id, "Emulator not running");
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
        var h = _environment.GetHostApp();
        if (h == null) return Err(id, "Emulator not initialized");
        var sys = h.CurrentRunningSystem;
        if (sys == null) return Err(id, "Emulator not running");

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
        var h = _environment.GetHostApp();
        if (h == null) return Err(id, "Emulator not initialized");
        var png = h.CaptureScreenshotPng();
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

    private RemoteCommandResult HandleEmuQuit(int? id)
    {
        if (!_environment.SupportsQuit)
            return Err(id, "emu.quit is not supported in this environment");
        var h = _environment.GetHostApp();
        if (h == null) return Err(id, "Emulator not initialized");
        _environment.RunOnUiThread(() => h.QuitApplication());
        return Ok(id);
    }

    // --- UI-thread dispatch ---

    private async Task<RemoteCommandResult> HandleUiAsync(int? id, Func<IRemotableHostApp, Task> action)
    {
        var h = _environment.GetHostApp();
        if (h == null) return Err(id, "Emulator not initialized");

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _environment.RunOnUiThread(async () =>
        {
            try
            {
                await action(h);
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
        var h = _environment.GetHostApp();
        if (h == null) return Err(id, "Emulator not initialized");

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.EnqueueRemoteAction(() =>
        {
            try
            {
                action(h);
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

    private static void MemWriteDirect(IRemotableHostApp h, RemoteCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Addr))
            throw new ArgumentException("Missing 'addr'");
        if (!ushort.TryParse(cmd.Addr, System.Globalization.NumberStyles.HexNumber, null, out var addr))
            throw new ArgumentException($"Invalid hex address: {cmd.Addr}");
        if (cmd.Data == null || cmd.Data.Value.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("'data' must be a JSON array of byte values");

        var sys = h.CurrentRunningSystem ?? throw new InvalidOperationException("Emulator not running");
        int i = 0;
        foreach (var elem in cmd.Data.Value.EnumerateArray())
            sys.Mem[(ushort)((addr + i++) & 0xFFFF)] = (byte)elem.GetInt32();
    }

    private static void JoystickSetDirect(IRemotableHostApp h, RemoteCommand cmd)
    {
        var input = GetInputProvider(h);
        int port = cmd.Port ?? 1;

        SetJoystickBool(input, port, "Up",    cmd.Up);
        SetJoystickBool(input, port, "Down",  cmd.Down);
        SetJoystickBool(input, port, "Left",  cmd.Left);
        SetJoystickBool(input, port, "Right", cmd.Right);
        SetJoystickBool(input, port, "Fire",  cmd.Fire);
    }

    private static void JoystickPressDirect(IRemotableHostApp h, RemoteCommand cmd)
    {
        var input = GetInputProvider(h);
        int port = cmd.Port ?? 1;

        SetHeldJoystickBool(input, port, "Up", cmd.Up, pressed: true);
        SetHeldJoystickBool(input, port, "Down", cmd.Down, pressed: true);
        SetHeldJoystickBool(input, port, "Left", cmd.Left, pressed: true);
        SetHeldJoystickBool(input, port, "Right", cmd.Right, pressed: true);
        SetHeldJoystickBool(input, port, "Fire", cmd.Fire, pressed: true);
    }

    private static void JoystickReleaseDirect(IRemotableHostApp h, RemoteCommand cmd)
    {
        var input = GetInputProvider(h);
        int port = cmd.Port ?? 1;

        SetHeldJoystickBool(input, port, "Up", cmd.Up, pressed: false);
        SetHeldJoystickBool(input, port, "Down", cmd.Down, pressed: false);
        SetHeldJoystickBool(input, port, "Left", cmd.Left, pressed: false);
        SetHeldJoystickBool(input, port, "Right", cmd.Right, pressed: false);
        SetHeldJoystickBool(input, port, "Fire", cmd.Fire, pressed: false);
    }

    private static void JoystickReleaseAllDirect(IRemotableHostApp h, RemoteCommand cmd)
    {
        var input = GetInputProvider(h);
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

    private static void KeyboardPressDirect(IRemotableHostApp h, RemoteCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.Key)) throw new ArgumentException("Missing 'key' parameter");
        var input = GetInputProvider(h);
        input.HoldKey(cmd.Key);
    }

    private static void KeyboardReleaseDirect(IRemotableHostApp h, RemoteCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.Key)) throw new ArgumentException("Missing 'key' parameter");
        var input = GetInputProvider(h);
        input.ReleaseHeldKey(cmd.Key);
    }

    private static void KeyboardReleaseAllDirect(IRemotableHostApp h)
    {
        GetInputProvider(h).ReleaseAllHeldKeys();
    }

    private RemoteCommandResult HandleKeyIsDown(int? id, RemoteCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.Key)) return Err(id, "Missing 'key' parameter");
        var h = _environment.GetHostApp();
        if (h == null) return Err(id, "Emulator not initialized");
        var sys = h.CurrentRunningSystem;
        if (sys == null) return Err(id, "Emulator not running");
        var input = sys.InputInjector;
        if (input == null) return Err(id, "System has no input injector");
        return new RemoteCommandResult { Id = id, Ok = true, IsDown = input.IsKeyDown(cmd.Key) };
    }

    private RemoteCommandResult HandleKeyGetAll(int? id)
    {
        var h = _environment.GetHostApp();
        if (h == null) return Err(id, "Emulator not initialized");
        var sys = h.CurrentRunningSystem;
        if (sys == null) return Err(id, "Emulator not running");
        var input = sys.InputInjector;
        if (input == null) return Err(id, "System has no input injector");
        return new RemoteCommandResult { Id = id, Ok = true, Data = input.GetAvailableKeys() };
    }

    private static IInputInjector GetInputProvider(IRemotableHostApp h)
    {
        var sys = h.CurrentRunningSystem ?? throw new InvalidOperationException("Emulator not running");
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
        var h = _environment.GetHostApp();
        if (h == null) { error = Err(id, "Emulator not initialized"); return null; }
        var sys = h.CurrentRunningSystem;
        if (sys == null) { error = Err(id, "Emulator not running"); return null; }
        if (sys is not C64 c64) { error = Err(id, "Current system is not a C64"); return null; }
        return c64;
    }

    // --- Helpers ---

    private static RemoteCommandResult Ok(int? id) =>
        new() { Id = id, Ok = true };

    private static RemoteCommandResult Err(int? id, string error) =>
        new() { Id = id, Ok = false, Error = error };
}
