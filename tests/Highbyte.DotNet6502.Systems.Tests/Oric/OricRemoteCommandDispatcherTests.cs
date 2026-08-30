using System.Text;
using System.Text.Json;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Remoting.Protocol;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using Highbyte.DotNet6502.Systems.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public class OricRemoteCommandDispatcherTests
{
    [Fact]
    public async Task Generic_Keyboard_Commands_Reach_The_Oric_Matrix()
    {
        var (oric, dispatcher) = Build();
        var inputHandler = new OricInputHandler(oric);
        inputHandler.Init(new TestHostInputState());

        var press = await dispatcher.DispatchAsync(new RemoteCommand { Cmd = "keyboard.press", Key = "h" });
        inputHandler.BeforeFrame();

        Assert.True(press.Ok);
        Assert.True(oric.Keyboard.IsKeyPressed(HostKey.KeyH));

        var release = await dispatcher.DispatchAsync(new RemoteCommand { Cmd = "keyboard.release", Key = "h" });
        inputHandler.BeforeFrame();
        Assert.True(release.Ok);
        Assert.False(oric.Keyboard.IsKeyPressed(HostKey.KeyH));
    }

    [Fact]
    public async Task Generic_Joystick_Commands_Reach_The_Selected_Oric_Interface()
    {
        var oric = new OricMachine(
            new OricConfig { JoystickInterface = OricJoystickInterface.PASE },
            NullLoggerFactory.Instance);
        var dispatcher = BuildDispatcher(oric);
        var inputHandler = new OricInputHandler(oric);
        inputHandler.Init(new TestHostInputState());

        var result = await dispatcher.DispatchAsync(new RemoteCommand
        {
            Cmd = "joystick.press",
            Port = 2,
            Left = true,
            Fire = true,
        });
        inputHandler.BeforeFrame();

        Assert.True(result.Ok);
        Assert.Contains(JoystickAction.Left, oric.Joystick.CurrentJoystickActions[2]);
        Assert.Contains(JoystickAction.Fire, oric.Joystick.CurrentJoystickActions[2]);
    }

    [Fact]
    public async Task LoadTap_Directly_Loads_The_First_Record()
    {
        var (oric, dispatcher) = Build();
        var payload = new byte[] { 0x11, 0x22, 0x33 };
        var tap = BuildTap(OricTapFile.MachineCodeFileType, payload, startAddress: 0x0600);

        var result = await dispatcher.DispatchAsync(CommandWithData("oric.loadtap", tap));

        Assert.True(result.Ok);
        for (var index = 0; index < payload.Length; index++)
            Assert.Equal(payload[index], oric.Mem[(ushort)(0x0600 + index)]);
        Assert.False(oric.Tape.IsInserted);
    }

    [Fact]
    public async Task Tape_Transport_Commands_Insert_Report_Rewind_And_Eject()
    {
        var (oric, dispatcher) = Build();
        var tap = BuildTap(OricTapFile.BasicFileType, [0x00, 0x00, 0x00]);

        var insert = await dispatcher.DispatchAsync(CommandWithData("oric.inserttape", tap));
        var status = await dispatcher.DispatchAsync(new RemoteCommand { Cmd = "oric.tapestatus" });

        Assert.True(insert.Ok);
        Assert.True(status.Ok);
        var data = Assert.IsType<Dictionary<string, object?>>(status.Data);
        Assert.Equal(true, data["inserted"]);
        Assert.Equal(tap.Length, data["length"]);
        var files = Assert.IsType<Dictionary<string, object?>[]>(data["files"]);
        Assert.Equal("TEST", files[0]["name"]);
        Assert.Equal("basic", files[0]["type"]);

        Assert.True((await dispatcher.DispatchAsync(new RemoteCommand { Cmd = "oric.rewindtape" })).Ok);
        Assert.Equal(0, oric.Tape.Position);

        Assert.True((await dispatcher.DispatchAsync(new RemoteCommand { Cmd = "oric.ejecttape" })).Ok);
        Assert.False(oric.Tape.IsInserted);
    }

    [Fact]
    public async Task Type_And_Basic_Queries_Are_Available_For_Oric()
    {
        var (oric, dispatcher) = Build();
        oric.Mem[OricMachine.KeyboardCharacterLatchAddress] = 0;

        var typeResult = await dispatcher.DispatchAsync(new RemoteCommand { Cmd = "oric.type", Text = "A" });
        oric.ExecuteOneFrame();
        var readyResult = await dispatcher.DispatchAsync(new RemoteCommand { Cmd = "oric.isbasicstarted" });
        var sourceResult = await dispatcher.DispatchAsync(new RemoteCommand { Cmd = "oric.getbasicsource" });

        Assert.True(typeResult.Ok);
        Assert.Equal((byte)('A' | 0x80), oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);
        Assert.True(readyResult.Ok);
        Assert.False(readyResult.IsBasicStarted);
        Assert.True(sourceResult.Ok);
        Assert.Equal(string.Empty, sourceResult.Data);
    }

    private static (OricMachine Oric, RemoteCommandDispatcher Dispatcher) Build()
    {
        var oric = new OricMachine(new OricConfig(), NullLoggerFactory.Instance);
        return (oric, BuildDispatcher(oric));
    }

    private static RemoteCommandDispatcher BuildDispatcher(OricMachine oric)
    {
        var hostApp = new FakeHostApp(oric);
        return new RemoteCommandDispatcher(new FakeEnvironment(hostApp));
    }

    private static RemoteCommand CommandWithData(string command, byte[] data)
        => new() { Cmd = command, Data = JsonSerializer.SerializeToElement(Convert.ToBase64String(data)) };

    private static byte[] BuildTap(byte fileType, byte[] payload, ushort startAddress = OricMachine.BasicProgramDefaultStartAddress)
    {
        var endAddress = (ushort)(startAddress + payload.Length - 1);
        return
        [
            0x16, 0x16, 0x16, 0x24,
            0x00, 0x00, fileType, 0x00,
            (byte)(endAddress >> 8), (byte)endAddress,
            (byte)(startAddress >> 8), (byte)startAddress,
            0x00,
            .. Encoding.ASCII.GetBytes("TEST"), 0x00,
            .. payload,
        ];
    }

    private sealed class FakeEnvironment(IRemotableHostApp hostApp) : IRemoteControlEnvironment
    {
        public IRemotableHostApp? GetHostApp() => hostApp;
        public void RunOnUiThread(Action action) => action();
        public bool SupportsQuit => false;
        public void DisplayRemoteMessage(string text, string level) { }
    }

    private sealed class FakeHostApp(OricMachine oric) : IRemotableHostApp
    {
        public string HostName => "Test";
        public string SelectedSystemName => OricMachine.SystemName;
        public HashSet<string> AvailableSystemNames => [OricMachine.SystemName];
        public string SelectedSystemConfigurationVariant => "ATMOS48K";
        public List<string> AllSelectedSystemConfigurationVariants => ["ATMOS48K"];
        public SystemRunner? CurrentSystemRunner => null;
        public ISystem? CurrentRunningSystem => oric;
        public EmulatorState EmulatorState => EmulatorState.Running;
        public IHostSystemConfig CurrentHostSystemConfig => null!;
        public bool CanSnapshotCurrentSystem => false;
        public bool SelectedSystemSupportsSnapshots => false;

        public void EnqueueRemoteAction(Action action) => action();
        public byte[]? CaptureScreenshotPng() => null;
        public Task SelectSystem(string systemName) => Task.CompletedTask;
        public Task SelectSystemConfigurationVariant(string configurationVariant) => Task.CompletedTask;
        public Task Start() => Task.CompletedTask;
        public void Pause() { }
        public void Stop() { }
        public void QuitApplication() { }
        public Task Reset() => Task.CompletedTask;
        public void RunEmulatorOneFrame() => oric.ExecuteOneFrame();
        public Task StepEmulatorFramesAsync(int frameCount) => Task.CompletedTask;
        public Task<(bool IsValid, List<string> Errors)> IsCurrentSystemConfigValid() =>
            Task.FromResult<(bool, List<string>)>((true, []));
        public Task<(bool IsValid, List<string> Errors)> IsSystemConfigValid(string systemName) =>
            Task.FromResult<(bool, List<string>)>((true, []));
        public Task<StoragePathsInfo> GetStoragePathsInfoAsync() => Task.FromResult(new StoragePathsInfo());
        public Task<bool> IsAudioSupported() => Task.FromResult(false);
        public Task<bool> IsAudioEnabled() => Task.FromResult(false);
        public Task<ISystem?> GetSelectedSystem() => Task.FromResult<ISystem?>(oric);
        public void UpdateHostSystemConfig(IHostSystemConfig newConfig) { }
        public Task PersistCurrentHostSystemConfig() => Task.CompletedTask;
        public Task SaveSnapshotAsync(Stream output, SnapshotSaveOptions? options = null, bool includeConfig = false) =>
            throw new NotSupportedException();
        public Task<SnapshotRestoreResult> LoadSnapshotAsync(Stream input, bool applyConfig = false) =>
            throw new NotSupportedException();
    }

    private sealed class TestHostInputState : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; } = new HashSet<HostKey>();
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();
        public bool CapsLockOn => false;
        public void UpdatePerFrame() { }
    }
}
