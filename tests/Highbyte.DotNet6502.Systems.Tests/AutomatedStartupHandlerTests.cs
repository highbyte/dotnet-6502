using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Rendering;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests;

public class AutomatedStartupHandlerTests
{
    [Fact]
    public async Task LoadPrgWithoutRunLoadedProgram_DoesNotRedirectProgramCounter()
    {
        var hostApp = new FakeHostApp();
        var request = new AutomatedStartupRequest(
            FakeHostApp.SystemName,
            null,
            true,
            false,
            null,
            false,
            false);

        hostApp.System.CPU.PC = 0x1234;

        await AutomatedStartupHandler.ExecuteAsync(
            hostApp,
            request,
            null,
            loggerFactory: NullLoggerFactory.Instance,
            loadPrgBytesProvider: () => Task.FromResult(CreatePrgBytes(0x2000, 0xEA, 0x60)));

        Assert.Equal((ushort)0x1234, hostApp.System.CPU.PC);
        Assert.Equal((byte)0xEA, hostApp.System.Mem[0x2000]);
        Assert.Equal((byte)0x60, hostApp.System.Mem[0x2001]);
    }

    [Fact]
    public async Task RunLoadedProgram_UsesParticipantWhenHandled()
    {
        var hostApp = new FakeHostApp();
        var participant = new RecordingStartupParticipant(handleLoadedProgram: true);
        var request = new AutomatedStartupRequest(
            FakeHostApp.SystemName,
            null,
            true,
            false,
            null,
            true,
            false);

        hostApp.System.CPU.PC = 0x1234;

        await AutomatedStartupHandler.ExecuteAsync(
            hostApp,
            request,
            null,
            loggerFactory: NullLoggerFactory.Instance,
            loadPrgBytesProvider: () => Task.FromResult(CreatePrgBytes(0x2000, 0xEA)),
            startupParticipant: participant);

        Assert.True(participant.OnSystemReadyCalled);
        Assert.Equal((ushort)0x2000, participant.HandledLoadAddress);
        Assert.Equal((ushort)0x1234, hostApp.System.CPU.PC);
    }

    [Fact]
    public async Task RunLoadedProgram_FallsBackToLoadAddressWhenParticipantDoesNotHandle()
    {
        var hostApp = new FakeHostApp();
        var participant = new RecordingStartupParticipant(handleLoadedProgram: false);
        var request = new AutomatedStartupRequest(
            FakeHostApp.SystemName,
            null,
            true,
            false,
            null,
            true,
            false);

        hostApp.System.CPU.PC = 0x1234;

        await AutomatedStartupHandler.ExecuteAsync(
            hostApp,
            request,
            null,
            loggerFactory: NullLoggerFactory.Instance,
            loadPrgBytesProvider: () => Task.FromResult(CreatePrgBytes(0x2000, 0xEA)),
            startupParticipant: participant);

        Assert.True(participant.OnSystemReadyCalled);
        Assert.Equal((ushort)0x2000, participant.HandledLoadAddress);
        Assert.Equal((ushort)0x2000, hostApp.System.CPU.PC);
    }

    private static byte[] CreatePrgBytes(ushort loadAddress, params byte[] data)
    {
        var bytes = new byte[data.Length + 2];
        bytes[0] = (byte)(loadAddress & 0xff);
        bytes[1] = (byte)(loadAddress >> 8);
        Array.Copy(data, 0, bytes, 2, data.Length);
        return bytes;
    }

    private sealed class RecordingStartupParticipant(bool handleLoadedProgram) : IAutomatedStartupParticipant
    {
        public string SystemName => FakeHostApp.SystemName;
        public bool OnSystemReadyCalled { get; private set; }
        public ushort? HandledLoadAddress { get; private set; }

        public Task OnSystemReadyAsync(
            IHostApp hostApp,
            AutomatedStartupRequest request,
            AutomatedStartupContext context)
        {
            OnSystemReadyCalled = true;
            return Task.CompletedTask;
        }

        public Task<bool> TryRunLoadedProgramAsync(
            IHostApp hostApp,
            AutomatedStartupRequest request,
            AutomatedStartupContext context,
            ushort loadAddress)
        {
            HandledLoadAddress = loadAddress;
            return Task.FromResult(handleLoadedProgram);
        }
    }

    private sealed class FakeHostApp : IHostApp
    {
        public const string SystemName = "TestSystem";
        public FakeSystem System { get; } = new();

        public string SelectedSystemName { get; private set; } = string.Empty;
        public HashSet<string> AvailableSystemNames { get; } = new(StringComparer.Ordinal) { SystemName };
        public string SelectedSystemConfigurationVariant { get; private set; } = "Default";
        public List<string> AllSelectedSystemConfigurationVariants { get; } = ["Default"];
        public SystemRunner? CurrentSystemRunner => null;
        public ISystem? CurrentRunningSystem { get; private set; }
        public EmulatorState EmulatorState { get; private set; } = EmulatorState.Uninitialized;
        public IHostSystemConfig CurrentHostSystemConfig { get; } = new FakeHostSystemConfig();

        public Task SelectSystem(string systemName)
        {
            SelectedSystemName = systemName;
            return Task.CompletedTask;
        }

        public Task SelectSystemConfigurationVariant(string configurationVariant)
        {
            SelectedSystemConfigurationVariant = configurationVariant;
            return Task.CompletedTask;
        }

        public Task Start()
        {
            CurrentRunningSystem = System;
            EmulatorState = EmulatorState.Running;
            return Task.CompletedTask;
        }

        public void Pause() => EmulatorState = EmulatorState.Paused;
        public void Stop()
        {
            EmulatorState = EmulatorState.Uninitialized;
            CurrentRunningSystem = null;
        }

        public void QuitApplication()
        {
        }

        public Task Reset() => Task.CompletedTask;
        public void RunEmulatorOneFrame()
        {
        }

        public Task<(bool IsValid, List<string> Errors)> IsCurrentSystemConfigValid()
            => Task.FromResult<(bool, List<string>)>((true, []));

        public Task<bool> IsAudioSupported() => Task.FromResult(false);
        public Task<bool> IsAudioEnabled() => Task.FromResult(false);
        public Task<ISystem?> GetSelectedSystem() => Task.FromResult<ISystem?>(System);
        public void UpdateHostSystemConfig(IHostSystemConfig newConfig)
        {
        }

        public Task PersistCurrentHostSystemConfig() => Task.CompletedTask;
    }

    private sealed class FakeSystem : ISystem
    {
        public string Name => FakeHostApp.SystemName;
        public List<string> SystemInfo { get; } = [];
        public List<KeyValuePair<string, Func<string>>> DebugInfo { get; } = [];
        public CPU CPU { get; } = new();
        public Memory Mem { get; } = new();
        public IScreen Screen { get; } = new FakeScreen();
        public bool InstrumentationEnabled { get; set; }
        public Instrumentations Instrumentations { get; } = new();
        public IRenderProvider? RenderProvider => null;
        public List<IRenderProvider> RenderProviders { get; } = [];

        public ExecEvaluatorTriggerResult ExecuteOneFrame(IExecEvaluator? execEvaluator = null)
            => ExecEvaluatorTriggerResult.NotTriggered;

        public ExecEvaluatorTriggerResult ExecuteOneInstruction(
            out InstructionExecResult instructionExecResult,
            IExecEvaluator? execEvaluator = null)
        {
            instructionExecResult = new InstructionExecResult();
            return ExecEvaluatorTriggerResult.NotTriggered;
        }
    }

    private sealed class FakeScreen : IScreen
    {
        public int DrawableAreaWidth => 320;
        public int DrawableAreaHeight => 200;
        public int VisibleWidth => 320;
        public int VisibleHeight => 200;
        public bool HasBorder => false;
        public int VisibleLeftRightBorderWidth => 0;
        public int VisibleTopBottomBorderHeight => 0;
        public float RefreshFrequencyHz => 60f;
    }

    private sealed class FakeHostSystemConfig : IHostSystemConfig
    {
        public ISystemConfig SystemConfig { get; } = new FakeSystemConfig();
        public bool AudioSupported => false;

        public object Clone() => this;
        public void Validate()
        {
        }

        public bool IsValid(out List<string> validationErrors)
        {
            validationErrors = [];
            return true;
        }
    }

    private sealed class FakeSystemConfig : ISystemConfig
    {
        public string Name => "Fake";
        public bool IsDirty => false;
        public Type? RenderProviderType => null;
        public Type? RenderTargetType => null;
        public Type? AudioProviderType => null;
        public Type? AudioTargetType => null;
        public bool AudioEnabled { get; set; }

        public object Clone() => this;
        public void ClearDirty()
        {
        }

        public bool IsValid(out List<string> validationErrors)
        {
            validationErrors = [];
            return true;
        }

        public List<Type> GetSupportedRenderProviderTypes() => [];
        public void SetRenderProviderType(Type? renderProviderType)
        {
        }

        public void SetRenderTargetType(Type? renderTargetType)
        {
        }

        public List<Type> GetSupportedAudioProviderTypes() => [];
        public void SetAudioProviderType(Type? audioProviderType)
        {
        }

        public void SetAudioTargetType(Type? audioTargetType)
        {
        }

        public void Validate()
        {
        }
    }
}
