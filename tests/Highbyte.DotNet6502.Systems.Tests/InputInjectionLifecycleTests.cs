using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Rendering;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests;

public class InputInjectionLifecycleTests
{
    [Fact]
    public void C64InputInjector_ClearsFrameScopedInputButKeepsHeldKeys()
    {
        var c64 = BuildC64();
        var injector = Assert.IsType<C64InputInjector>(c64.InputInjector);

        injector.HoldKey("a");
        injector.KeyPress("b");
        injector.SetJoystickAction(1, "up", true);

        Assert.True(injector.IsKeyDown("a"));
        Assert.True(injector.IsKeyDown("b"));
        Assert.True(injector.IsJoystickActionDown(1, "up"));

        injector.BeginFrame();

        Assert.True(injector.IsKeyDown("a"));
        Assert.False(injector.IsKeyDown("b"));
        Assert.False(injector.IsJoystickActionDown(1, "up"));

        injector.ReleaseHeldKey("a");

        Assert.False(injector.IsKeyDown("a"));
    }

    [Fact]
    public void C64InputInjector_ClearsFrameScopedJoystickButKeepsHeldJoystickActions()
    {
        var c64 = BuildC64();
        var injector = Assert.IsType<C64InputInjector>(c64.InputInjector);

        injector.HoldJoystickAction(1, "up");
        injector.SetJoystickAction(1, "fire", true);

        Assert.True(injector.IsJoystickActionDown(1, "up"));
        Assert.True(injector.IsJoystickActionDown(1, "fire"));

        injector.BeginFrame();

        Assert.True(injector.IsJoystickActionDown(1, "up"));
        Assert.False(injector.IsJoystickActionDown(1, "fire"));

        injector.ReleaseHeldJoystickAction(1, "up");

        Assert.False(injector.IsJoystickActionDown(1, "up"));
    }

    [Fact]
    public async Task RunEmulatorOneFrame_BeginsFrameAndDrainsRemoteActionsBeforeScripting()
    {
        var systemList = new SystemList<NullInputHandlerContext, NullAudioHandlerContext>();
        var configurer = new FrameLifecycleSystemConfigurer();
        systemList.AddSystem(configurer);

        var app = new FrameLifecycleHostApp(systemList);
        app.SetContexts(() => new NullInputHandlerContext(), () => new NullAudioHandlerContext());
        app.InitInputHandlerContext();
        app.InitAudioHandlerContext();

        var observedState = new ObservedInputState();
        app.SetScriptingEngine(new RecordingScriptingEngine(() =>
        {
            observedState.StaleKeyDown = configurer.System.Injector.IsKeyDown("stale");
            observedState.FreshKeyDown = configurer.System.Injector.IsKeyDown("fresh");
            observedState.BeginFrameCallCount = configurer.System.Injector.BeginFrameCallCount;
        }));

        await app.SelectSystem(FrameLifecycleSystem.SystemName);
        await app.Start();

        configurer.System.Injector.KeyPress("stale");
        app.EnqueueRemoteAction(() => configurer.System.Injector.KeyPress("fresh"));

        app.RunEmulatorOneFrame();

        Assert.Equal(1, observedState.BeginFrameCallCount);
        Assert.False(observedState.StaleKeyDown);
        Assert.True(observedState.FreshKeyDown);
    }

    private static C64 BuildC64()
    {
        var c64Config = new C64Config
        {
            C64Model = "C64NTSC",
            Vic2Model = "NTSC",
            LoadROMs = false
        };

        return C64.BuildC64(c64Config, new NullLoggerFactory());
    }

    private sealed class ObservedInputState
    {
        public bool StaleKeyDown { get; set; }
        public bool FreshKeyDown { get; set; }
        public int BeginFrameCallCount { get; set; }
    }

    private sealed class FrameLifecycleHostApp : HostApp<NullInputHandlerContext, NullAudioHandlerContext>
    {
        public FrameLifecycleHostApp(SystemList<NullInputHandlerContext, NullAudioHandlerContext> systemList)
            : base("TestHost", systemList, new NullLoggerFactory())
        {
        }
    }

    private sealed class FrameLifecycleSystemConfigurer : ISystemConfigurer<NullInputHandlerContext, NullAudioHandlerContext>
    {
        public string SystemName => FrameLifecycleSystem.SystemName;

        public FrameLifecycleSystem System { get; } = new();

        public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig) => Task.FromResult(new List<string> { "DEFAULT" });

        public Task<IHostSystemConfig> GetNewHostSystemConfig() => Task.FromResult<IHostSystemConfig>(new FrameLifecycleHostSystemConfig());

        public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig) => Task.CompletedTask;

        public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig) => Task.FromResult<ISystem>(System);

        public Task<SystemRunner> BuildSystemRunner(
            ISystem system,
            IHostSystemConfig hostSystemConfig,
            NullInputHandlerContext inputHandlerContext,
            NullAudioHandlerContext audioHandlerContext)
        {
            var testSystem = (FrameLifecycleSystem)system;
            return Task.FromResult(new SystemRunner(
                testSystem,
                new FrameLifecycleInputHandler(testSystem),
                new NullAudioHandler(testSystem)));
        }
    }

    private sealed class FrameLifecycleHostSystemConfig : IHostSystemConfig
    {
        public ISystemConfig SystemConfig { get; } = new FrameLifecycleSystemConfig();

        public bool AudioSupported => false;

        public object Clone() => new FrameLifecycleHostSystemConfig();

        public void Validate()
        {
        }

        public bool IsValid(out List<string> validationErrors)
        {
            validationErrors = new List<string>();
            return true;
        }
    }

    private sealed class FrameLifecycleSystemConfig : ISystemConfig
    {
        public bool AudioSupported { get; set; }
        public bool AudioEnabled { get; set; }

        public Type? RenderProviderType { get; private set; }
        public Type? RenderTargetType { get; private set; }

        public object Clone() => new FrameLifecycleSystemConfig();

        public bool IsValid(out List<string> validationErrors)
        {
            validationErrors = new List<string>();
            return true;
        }

        public void Validate()
        {
        }

        public List<Type> GetSupportedRenderProviderTypes() => new List<Type> { typeof(NullRenderProvider) };

        public void SetRenderProviderType(Type? renderProviderType) => RenderProviderType = renderProviderType;

        public void SetRenderTargetType(Type renderTargetType) => RenderTargetType = renderTargetType;
    }

    private sealed class FrameLifecycleSystem : ISystem
    {
        public const string SystemName = "FrameLifecycleSystem";

        public string Name => SystemName;

        public List<string> SystemInfo => new();

        public List<KeyValuePair<string, Func<string>>> DebugInfo => new();

        public CPU CPU => throw new NotImplementedException();

        public Memory Mem => throw new NotImplementedException();

        public IScreen Screen => throw new NotImplementedException();

        public bool InstrumentationEnabled { get; set; }

        public Instrumentations Instrumentations { get; } = new();

        public IRenderProvider? RenderProvider => null;

        public List<IRenderProvider> RenderProviders { get; } = new();

        public RecordingInputInjector Injector { get; } = new();

        public IInputInjector? InputInjector => Injector;

        public ExecEvaluatorTriggerResult ExecuteOneFrame(IExecEvaluator? execEvaluator = null) => new();

        public ExecEvaluatorTriggerResult ExecuteOneInstruction(out InstructionExecResult instructionExecResult, IExecEvaluator? execEvaluator = null)
        {
            instructionExecResult = new InstructionExecResult();
            return new ExecEvaluatorTriggerResult();
        }
    }

    private sealed class FrameLifecycleInputHandler : IInputHandler
    {
        private readonly FrameLifecycleSystem _system;

        public FrameLifecycleInputHandler(FrameLifecycleSystem system)
        {
            _system = system;
        }

        public ISystem System => _system;

        public Instrumentations Instrumentations { get; } = new();

        public void Init()
        {
        }

        public void BeforeFrame()
        {
        }

        public void Cleanup()
        {
        }

        public List<string> GetDebugInfo() => new();
    }

    private sealed class RecordingInputInjector : IInputInjector
    {
        private readonly HashSet<string> _frameKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _heldKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, HashSet<string>> _heldJoystickActions = new()
        {
            { 1, new HashSet<string>(StringComparer.OrdinalIgnoreCase) },
            { 2, new HashSet<string>(StringComparer.OrdinalIgnoreCase) }
        };
        private readonly Dictionary<int, HashSet<string>> _frameJoystickActions = new()
        {
            { 1, new HashSet<string>(StringComparer.OrdinalIgnoreCase) },
            { 2, new HashSet<string>(StringComparer.OrdinalIgnoreCase) }
        };

        public int BeginFrameCallCount { get; private set; }

        public IReadOnlyList<string> GetAvailableKeys() => new[] { "stale", "fresh" };

        public IReadOnlyList<string> GetAvailableJoystickActions() => new[] { "up", "down", "left", "right", "fire" };

        public int JoystickPortCount => 2;

        public void BeginFrame()
        {
            BeginFrameCallCount++;
            _frameKeys.Clear();
            _frameJoystickActions[1].Clear();
            _frameJoystickActions[2].Clear();
        }

        public void KeyPress(string keyName) => _frameKeys.Add(keyName);

        public void KeyRelease(string keyName) => _frameKeys.Remove(keyName);

        public void KeyReleaseAll() => _frameKeys.Clear();

        public void HoldKey(string keyName) => _heldKeys.Add(keyName);

        public void ReleaseHeldKey(string keyName) => _heldKeys.Remove(keyName);

        public void ReleaseAllHeldKeys() => _heldKeys.Clear();

        public void HoldJoystickAction(int port, string actionName)
        {
            if (_heldJoystickActions.TryGetValue(port, out var actions))
                actions.Add(actionName);
        }

        public void ReleaseHeldJoystickAction(int port, string actionName)
        {
            if (_heldJoystickActions.TryGetValue(port, out var actions))
                actions.Remove(actionName);
        }

        public void ReleaseAllHeldJoystickActions(int port)
        {
            if (_heldJoystickActions.TryGetValue(port, out var actions))
                actions.Clear();
        }

        public bool IsKeyDown(string keyName) => _heldKeys.Contains(keyName) || _frameKeys.Contains(keyName);

        public void SetJoystickAction(int port, string actionName, bool pressed)
        {
            if (!_frameJoystickActions.TryGetValue(port, out var actions))
                return;

            if (pressed)
                actions.Add(actionName);
            else
                actions.Remove(actionName);
        }

        public bool IsJoystickActionDown(int port, string actionName)
            => (_heldJoystickActions.TryGetValue(port, out var heldActions) && heldActions.Contains(actionName))
                || (_frameJoystickActions.TryGetValue(port, out var frameActions) && frameActions.Contains(actionName));

        public void Clear()
        {
            _heldKeys.Clear();
            _heldJoystickActions[1].Clear();
            _heldJoystickActions[2].Clear();
            BeginFrame();
        }
    }

    private sealed class RecordingScriptingEngine : IScriptingEngine
    {
        private readonly Action _beforeFrameAction;

        public RecordingScriptingEngine(Action beforeFrameAction)
        {
            _beforeFrameAction = beforeFrameAction;
        }

        public bool IsEnabled => false;

        public string ScriptDirectory => string.Empty;

        public bool CanManageScripts => false;

        public event EventHandler? ScriptStatusChanged
        {
            add { }
            remove { }
        }

        public void LoadScripts()
        {
        }

        public void OnSystemStarted(ISystem system)
        {
        }

        public void InvokeBeforeFrame() => _beforeFrameAction();

        public void ResumeCoroutines()
        {
        }

        public void InvokeAfterFrame()
        {
        }

        public void InvokeEvent(string hookName, params object[] args)
        {
        }

        public void SetHostApp(IHostApp? hostApp)
        {
        }

        public Task DrainPendingActionsAsync() => Task.CompletedTask;

        public IReadOnlyList<ScriptStatus> GetScriptStatuses() => Array.Empty<ScriptStatus>();

        public void SetScriptEnabled(string fileName, bool enabled)
        {
        }

        public void ReloadScript(string fileName)
        {
        }

        public void ReloadAllScripts()
        {
        }

        public void UpsertScript(string fileName, string content)
        {
        }

        public void DeleteScript(string fileName)
        {
        }
    }
}