using System.IO.Compression;
using System.Text.Json.Nodes;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Recording;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Remoting.Protocol;
using Highbyte.DotNet6502.Impl.Avalonia.Monitor;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Scripting.MoonSharp;
using static Highbyte.DotNet6502.Systems.Tests.SystemConfigurerTests;

namespace Highbyte.DotNet6502.Systems.Tests.Recording;

public class HostRecordingTests
{
    [Fact]
    public async Task Active_recording_rejects_cartridge_images_and_resets_before_mutation()
    {
        var (host, _) = await BuildHostAsync();
        await host.Start();
        using var output = new MemoryStream();
        await host.StartDemoRecordingAsync(output);
        var machine = Assert.IsType<C64>(host.CurrentRunningSystem);
        var pc = machine.CPU.PC;
        Assert.Throws<InvalidOperationException>(() => machine.AttachCrtImage(Array.Empty<byte>()));
        Assert.Throws<InvalidOperationException>(machine.HardReset);
        Assert.Equal(pc, machine.CPU.PC);
        host.Close();
    }

    [Fact]
    public async Task Lua_disk_and_previously_queued_binary_loads_are_rejected_before_file_access()
    {
        var (host, _) = await BuildHostAsync();
        await host.Start();
        var pending = new List<Func<Task>>();
        var adapter = new MoonSharpScriptingEngineAdapter(NullLoggerFactory.Instance, "test");
        adapter.InitializeVm(host, (_, action) => pending.Add(action), new ScriptingConfig
        {
            AllowFileIO = true, FileBaseDirectory = Path.GetTempPath(), AllowStore = false,
            AllowHttpRequests = false, AllowTcpClient = false,
        }, NullLogger.Instance, () => 0, () => 0);
        var script = adapter.LoadScript("emu.load('missing-recording-test.prg')", "load.lua");
        adapter.InitialResume(script!);
        Assert.Single(pending);
        using var output = new MemoryStream();
        await host.StartDemoRecordingAsync(output);
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending[0]());
        var proxy = new LuaC64Proxy();
        proxy.SetC64((C64)host.CurrentRunningSystem!);
        Assert.Throws<InvalidOperationException>(() => proxy.load_d64("missing-recording-test.d64"));
        host.Close();
    }

    [Fact]
    public async Task Live_running_and_paused_steps_share_the_same_recorded_timeline()
    {
        var (host, input) = await BuildHostAsync();
        using var file = new MemoryStream();
        await host.Start();
        host.RunEmulatorOneFrame();
        await host.StartDemoRecordingAsync(file);
        var monitor = new AvaloniaMonitor(host.CurrentSystemRunner!, new MonitorConfig());
        Assert.Throws<InvalidOperationException>(() => monitor.Enable());
        Assert.Throws<InvalidOperationException>(() => monitor.ProcessCommand("r a $42"));
        input.Keys.Add(HostKey.KeyA);
        for (int i = 0; i < 3; i++) host.RunEmulatorOneFrame();
        host.Pause();
        input.Keys.Clear();
        await host.StepEmulatorFramesAsync(2);
        host.StopDemoRecording();
        Assert.Equal(5, host.DemoSession!.Frame);
        var expected = ((C64)host.CurrentRunningSystem!).RAM.ToArray();
        file.Position = 0;
        await host.LoadDemoAsync(file);
        Assert.Equal(EmulatorState.Paused, host.EmulatorState);
        Assert.True(host.DemoSession!.OwnsInput);
        input.Keys.Add(HostKey.KeyB);
        await host.StepEmulatorFramesAsync(10);
        Assert.Equal(PlaybackOutcome.Completed, host.DemoSession.Outcome);
        Assert.Equal(5, host.DemoSession.Frame);
        Assert.Equal(expected, ((C64)host.CurrentRunningSystem!).RAM);
        Assert.Equal(EmulatorState.Paused, host.EmulatorState);
        host.Close();
    }

    [Fact]
    public async Task Different_build_logs_once_and_first_checkpoint_mismatch_pauses_the_host()
    {
        var logs = new DotNet6502InMemLogStore();
        using var factory = LoggerFactory.Create(builder => builder.AddInMem(logs));
        var (host, _) = await BuildHostAsync(factory);
        using var file = new MemoryStream();
        await host.Start();
        await host.StartDemoRecordingAsync(file);
        for (int i = 0; i < 301; i++) host.RunEmulatorOneFrame();
        host.StopDemoRecording();

        using (var archive = new ZipArchive(file, ZipArchiveMode.Update, leaveOpen: true))
        using (var stream = archive.GetEntry("demo.json")!.Open())
        {
            var manifest = JsonNode.Parse(stream)!;
            manifest["recordedBuildId"] = "test-producer-build";
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(manifest.ToJsonString());
        }
        file.Position = 0;
        await host.LoadDemoAsync(file);
        await host.RestartDemoAsync();
        var info = Assert.Single(logs.GetFullLogMessages(), e => e.Message.Contains("Replay build differs"));
        Assert.Equal(LogLevel.Information, info.LogLevel);
        Assert.Contains("test-producer-build", info.Message);

        // Force a different emulated state while leaving the recorded input and checkpoints intact.
        ((C64)host.CurrentRunningSystem!).RAM[0x7000] ^= 1;
        await host.StepEmulatorFramesAsync(301);
        Assert.Equal(EmulatorState.Paused, host.EmulatorState);
        Assert.Equal(PlaybackOutcome.Failed, host.DemoSession!.Outcome);
        Assert.False(host.DemoSession.OwnsInput);
        Assert.Equal(300, host.DemoSession.Frame);
        var error = Assert.Single(logs.GetFullLogMessages(), e => e.LogLevel == LogLevel.Error);
        Assert.Matches(@"Replay diverged at frame 300 of 301: expected [0-9a-f]{64}, actual [0-9a-f]{64}", error.Message);
        host.Close();
    }

    [Fact]
    public async Task Playback_blocks_mutations_and_requires_fresh_input_after_stop()
    {
        var (host, input) = await BuildHostAsync();
        using var file = new MemoryStream();
        await host.Start();
        await host.StartDemoRecordingAsync(file);
        host.RunEmulatorOneFrame();
        host.StopDemoRecording();
        file.Position = 0;
        await host.LoadDemoAsync(file);
        Assert.Throws<InvalidOperationException>(() => host.EnsureMachineMutationAllowed());
        Assert.Throws<InvalidOperationException>(() => host.EnsureLiveInputAllowed());
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Reset());
        Assert.False(host.TryQueueKeyboardText("x"));
        input.Keys.Add(HostKey.KeyB);
        host.StopDemoPlayback();
        await host.Start();
        host.RunEmulatorOneFrame();
        var c64 = (C64)host.CurrentRunningSystem!;
        Assert.False(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.B));
        input.Keys.Clear();
        host.RunEmulatorOneFrame();
        input.Keys.Add(HostKey.KeyB);
        host.RunEmulatorOneFrame();
        Assert.True(c64.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.B));
        host.Close();
    }

    [Fact]
    public async Task Invalid_demo_does_not_replace_the_existing_machine_or_session()
    {
        var (host, _) = await BuildHostAsync();
        await host.Start();
        var original = host.CurrentRunningSystem;
        using var invalid = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidDataException>(() => host.LoadDemoAsync(invalid));
        Assert.Same(original, host.CurrentRunningSystem);
        Assert.Equal(EmulatorState.Running, host.EmulatorState);
        host.Close();
    }

    [Fact]
    public async Task Recording_start_failure_preserves_running_state()
    {
        var (host, _) = await BuildHostAsync();
        await host.Start();
        await using var output = new MemoryStream(new byte[1], writable: false);
        await Assert.ThrowsAsync<ArgumentException>(() => host.StartDemoRecordingAsync(output));
        Assert.Equal(EmulatorState.Running, host.EmulatorState);
        Assert.NotEqual(RecordingMode.Recording, host.DemoSession?.Mode);
        host.Close();
    }

    [Fact]
    public async Task Remote_recording_survives_client_changes_and_failed_save_then_blocks_replay_mutations()
    {
        var (host, _) = await BuildHostAsync();
        await host.Start();
        host.Pause();
        var environment = new TestRemoteEnvironment(host);
        var firstClient = new RemoteCommandDispatcher(environment);
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var originalPath = Path.Combine(directory, "original.d6502demo");
        var retryPath = Path.Combine(directory, "retry.d6502demo");
        try
        {
            Assert.True((await firstClient.DispatchAsync(new() { Cmd = "demo.record", Path = originalPath })).Ok);
            Assert.True((await firstClient.DispatchAsync(new() { Cmd = "emu.runframes", Count = 4 })).Ok);
            await File.WriteAllTextAsync(originalPath, "existing destination");
            var secondClient = new RemoteCommandDispatcher(environment);
            Assert.False((await secondClient.DispatchAsync(new() { Cmd = "demo.save" })).Ok);
            Assert.Equal("existing destination", await File.ReadAllTextAsync(originalPath));
            Assert.True((await secondClient.DispatchAsync(new() { Cmd = "demo.save", Path = retryPath })).Ok);
            Assert.True((await secondClient.DispatchAsync(new() { Cmd = "demo.open", Path = retryPath })).Ok);
            Assert.False((await secondClient.DispatchAsync(new() { Cmd = "keyboard.press", Key = "a" })).Ok);
            Assert.False((await secondClient.DispatchAsync(new() { Cmd = "cpu.set", A = 42 })).Ok);
            Assert.False((await secondClient.DispatchAsync(new() { Cmd = "mem.read", Addr = "D019", Len = 1 })).Ok);
            Assert.True((await secondClient.DispatchAsync(new() { Cmd = "cpu.get" })).Ok);
            Assert.True((await secondClient.DispatchAsync(new() { Cmd = "emu.runframes", Count = 10 })).Ok);
            var state = await secondClient.DispatchAsync(new() { Cmd = "demo.state" });
            Assert.Equal("Completed", state.DemoOutcome);
            Assert.Equal(4, state.DemoFrame);
        }
        finally
        {
            host.Close();
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TestRemoteEnvironment(IRemotableHostApp host) : IRemoteControlEnvironment
    {
        public IRemotableHostApp? GetHostApp() => host;
        public void RunOnUiThread(Action action) => action();
        public bool SupportsQuit => false;
        public void DisplayRemoteMessage(string text, string level) { }
    }

    private sealed class RecordingHost(SystemList systems, ILoggerFactory loggerFactory) : HostApp("RecordingTest", systems, loggerFactory), IRemotableHostApp
    {
        public byte[]? CaptureScreenshotPng() => null;
    }

    private static async Task<(RecordingHost Host, TestInput Input)> BuildHostAsync(ILoggerFactory? loggerFactory = null)
    {
        var systems = new SystemList();
        systems.AddSystem(new RecordingConfigurer());
        var host = new RecordingHost(systems, loggerFactory ?? NullLoggerFactory.Instance);
        var input = new TestInput();
        host.SetContexts(() => input);
        host.InitInputHandlerContext();
        await host.SelectSystem(C64.SystemName);
        return (host, input);
    }

    private sealed class TestInput : IInputHandlerContext, IHostInputState
    {
        public HashSet<HostKey> Keys { get; } = [];
        public IReadOnlySet<HostKey> KeysDown => Keys;
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();
        public bool CapsLockOn => false;
        public bool IsInitialized { get; private set; }
        public void Init() => IsInitialized = true;
        public void Cleanup() { }
        public void UpdatePerFrame() { }
        public string? DetectNativeKeyboardLayoutId() => null;
    }

    private sealed class RecordingConfigurer : ISystemConfigurer
    {
        public string SystemName => C64.SystemName;
        public Task<List<string>> GetConfigurationVariants(ISystemConfig config) => Task.FromResult<List<string>>(["C64PAL"]);
        public Task<IHostSystemConfig> GetNewHostSystemConfig() => Task.FromResult<IHostSystemConfig>(new TestHostSystemConfig());
        public Task PersistHostSystemConfig(IHostSystemConfig config) => Task.CompletedTask;
        public Task<ISystem> BuildSystem(string variant, ISystemConfig config)
        {
            var c64 = C64.BuildC64(new C64Config
            {
                LoadROMs = false, C64Model = variant, Vic2Model = "PAL", AudioEnabled = false,
            }, NullLoggerFactory.Instance);
            c64.Mem.StoreData(0xC000, new byte[] { 0xAD, 0x01, 0xDC, 0x8D, 0x00, 0x40, 0x4C, 0x00, 0xC0 });
            c64.CPU.PC = 0xC000;
            c64.Mem[0xDC02] = 0xFF;
            c64.Mem[0xDC00] = 0;
            return Task.FromResult<ISystem>(c64);
        }
        public Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig config)
        {
            var c64 = (C64)system;
            c64.InputConsumer = new C64InputHandler(c64, NullLoggerFactory.Instance,
                new C64InputConfig { KeyboardLayout = C64KeyboardLayout.US });
            return Task.FromResult(new SystemRunner(c64));
        }
    }
}
