using System.IO.Compression;
using System.Net;
using System.Text;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Systems.Oric.Render;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using Highbyte.DotNet6502.Systems.Oric.Tape.Download;
using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricAutoLoadAndRunTests
{
    [Fact]
    public async Task DownloadAndRunExtractsTapFromZipAndUsesCorsProxy()
    {
        var tapBytes = BuildTap(autoRunFlag: 0xc7);
        var handler = new ByteResponseHandler(BuildZip("games/demo.tap", tapBytes));
        var oric = BuildReadyOric();
        var hostApp = new FakeHostApp(oric, EmulatorState.Running);
        var loader = new OricAutoLoadAndRun(
            NullLoggerFactory.Instance,
            new HttpClient(handler),
            hostApp,
            corsProxyUrl: "https://proxy.example/fetch?url=");
        var programInfo = new OricDownloadProgramInfo(
            "Demo",
            "https://downloads.example/demo.zip",
            zipEntryName: "games/demo.tap");

        await loader.DownloadAndRunProgram(programInfo);

        Assert.Equal(
            "https://proxy.example/fetch?url=" + Uri.EscapeDataString(programInfo.DownloadUrl),
            handler.RequestUri?.AbsoluteUri);
        Assert.True(oric.Tape.IsInserted);
        Assert.Single(oric.Tape.Files);
        Assert.Equal("TEST", oric.Tape.Files[0].Name);
        Assert.Equal(1, hostApp.StopCount);
        Assert.Equal(1, hostApp.PauseCount);
        Assert.Equal(2, hostApp.StartCount);
        AssertQueuedText(oric, "CLOAD\"\"\r");
    }

    [Fact]
    public async Task NonAutoRunBasicQueuesRunAfterCload()
    {
        var oric = await DownloadTapAsync(BuildTap());

        AssertQueuedText(oric, "CLOAD\"\"\rRUN\r");
    }

    [Fact]
    public async Task NonAutoRunMachineCodeQueuesCallAtTheLoadAddress()
    {
        var oric = await DownloadTapAsync(BuildTap(
            fileType: OricTapFile.MachineCodeFileType,
            startAddress: 0x0600));

        AssertQueuedText(oric, "CLOAD\"\"\rCALL#0600\r");
    }

    [Fact]
    public async Task LoadAndRunTapUsesEmbeddedBytesAndAppliesConfigurationBeforeStarting()
    {
        var tapBytes = BuildTap(
            fileType: OricTapFile.MachineCodeFileType,
            autoRunFlag: 0xc7,
            startAddress: 0x0600);
        var oric = BuildReadyOric();
        var hostApp = new FakeHostApp(oric, EmulatorState.Running);
        var loader = new OricAutoLoadAndRun(
            NullLoggerFactory.Instance,
            new HttpClient(new ByteResponseHandler([])),
            hostApp);
        var configured = false;

        await loader.LoadAndRunTap(
            "Raster bars",
            tapBytes,
            () =>
            {
                configured = true;
                Assert.Equal(EmulatorState.Uninitialized, hostApp.EmulatorState);
                Assert.Equal(0, hostApp.StartCount);
                return Task.CompletedTask;
            });

        Assert.True(configured);
        Assert.True(oric.Tape.IsInserted);
        Assert.Equal("Raster bars", oric.Tape.SourceName);
        Assert.Equal(1, hostApp.StopCount);
        Assert.Equal(1, hostApp.PauseCount);
        Assert.Equal(2, hostApp.StartCount);
        AssertQueuedText(oric, "CLOAD\"\"\r");
    }

    [Fact]
    public async Task AppliesProgramConfigurationAfterStoppingAndBeforeStarting()
    {
        var oric = BuildReadyOric();
        var hostApp = new FakeHostApp(oric, EmulatorState.Running);
        var loader = new OricAutoLoadAndRun(
            NullLoggerFactory.Instance,
            new HttpClient(new ByteResponseHandler(BuildTap(autoRunFlag: 0xc7))),
            hostApp);
        var programInfo = new OricDownloadProgramInfo(
            "Joystick game",
            "https://downloads.example/joystick.tap",
            joystickInterface: OricJoystickInterface.IJK,
            keyboardJoystickEnabled: true,
            keyboardJoystickNumber: 1,
            vSyncHackEnabled: true);
        var callbackCount = 0;

        await loader.DownloadAndRunProgram(
            programInfo,
            configuredProgram =>
            {
                callbackCount++;
                Assert.Same(programInfo, configuredProgram);
                Assert.True(configuredProgram.VSyncHackEnabled);
                Assert.Equal(EmulatorState.Uninitialized, hostApp.EmulatorState);
                Assert.Equal(0, hostApp.StartCount);
                return Task.CompletedTask;
            });

        Assert.Equal(1, callbackCount);
        Assert.Equal(2, hostApp.StartCount);
    }

    [Fact]
    public void DownloadProgramInfoDefaultsToKeyboardOnlyAndValidatesJoystickPort()
    {
        var programInfo = new OricDownloadProgramInfo(
            "Keyboard game",
            "https://downloads.example/keyboard.tap");

        Assert.Equal(OricJoystickInterface.None, programInfo.JoystickInterface);
        Assert.False(programInfo.KeyboardJoystickEnabled);
        Assert.Equal(1, programInfo.KeyboardJoystickNumber);
        Assert.False(programInfo.VSyncHackEnabled);
        Assert.Throws<ArgumentOutOfRangeException>(() => new OricDownloadProgramInfo(
            "Invalid",
            "https://downloads.example/invalid.tap",
            keyboardJoystickNumber: 3));
    }

    private static async Task<OricMachine> DownloadTapAsync(byte[] tapBytes)
    {
        var oric = BuildReadyOric();
        var hostApp = new FakeHostApp(oric, EmulatorState.Uninitialized);
        var loader = new OricAutoLoadAndRun(
            NullLoggerFactory.Instance,
            new HttpClient(new ByteResponseHandler(tapBytes)),
            hostApp);

        await loader.DownloadAndRunProgram(
            new OricDownloadProgramInfo("Demo", "https://downloads.example/demo.tap"));

        return oric;
    }

    private static OricMachine BuildReadyOric()
    {
        var oric = new OricMachine();
        oric.Mem.StoreData(OricRasterizer.TextScreenAddress, "READY"u8.ToArray());
        return oric;
    }

    private static void AssertQueuedText(OricMachine oric, string expected)
    {
        foreach (var character in expected)
        {
            oric.Mem[OricMachine.KeyboardCharacterLatchAddress] = 0;
            oric.ExecuteOneFrame();
            var expectedValue = character == '\r' ? 0x0d : character;
            Assert.Equal((byte)(expectedValue | 0x80), oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);
        }

        oric.Mem[OricMachine.KeyboardCharacterLatchAddress] = 0;
        oric.ExecuteOneFrame();
        Assert.Equal(0, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);
    }

    private static byte[] BuildTap(
        byte fileType = OricTapFile.BasicFileType,
        byte autoRunFlag = 0,
        ushort startAddress = OricMachine.BasicProgramDefaultStartAddress)
    {
        byte[] payload = [0, 0, 0];
        var endAddress = (ushort)(startAddress + payload.Length - 1);
        return
        [
            0x16, 0x16, 0x16, 0x24,
            0x00, 0x00, fileType, autoRunFlag,
            (byte)(endAddress >> 8), (byte)endAddress,
            (byte)(startAddress >> 8), (byte)startAddress,
            0x00,
            .. Encoding.ASCII.GetBytes("TEST"), 0x00,
            .. payload,
        ];
    }

    private static byte[] BuildZip(string entryName, byte[] content)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var stream = entry.Open();
            stream.Write(content);
        }
        return output.ToArray();
    }

    private sealed class ByteResponseHandler(byte[] responseBytes) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseBytes),
            });
        }
    }

    private sealed class FakeHostApp(OricMachine oric, EmulatorState initialState) : IHostApp
    {
        public int StartCount { get; private set; }
        public int PauseCount { get; private set; }
        public int StopCount { get; private set; }

        public string HostName => "Test";
        public string SelectedSystemName => OricMachine.SystemName;
        public HashSet<string> AvailableSystemNames => [OricMachine.SystemName];
        public string SelectedSystemConfigurationVariant => "ATMOS48K";
        public List<string> AllSelectedSystemConfigurationVariants => ["ATMOS48K"];
        public SystemRunner? CurrentSystemRunner => null;
        public ISystem? CurrentRunningSystem => oric;
        public EmulatorState EmulatorState { get; private set; } = initialState;
        public IHostSystemConfig CurrentHostSystemConfig => null!;
        public bool CanSnapshotCurrentSystem => false;
        public bool SelectedSystemSupportsSnapshots => false;

        public Task Start()
        {
            StartCount++;
            EmulatorState = EmulatorState.Running;
            return Task.CompletedTask;
        }

        public void Pause()
        {
            PauseCount++;
            EmulatorState = EmulatorState.Paused;
        }

        public void Stop()
        {
            StopCount++;
            EmulatorState = EmulatorState.Uninitialized;
        }

        public Task SelectSystem(string systemName) => Task.CompletedTask;
        public Task SelectSystemConfigurationVariant(string configurationVariant) => Task.CompletedTask;
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
}
