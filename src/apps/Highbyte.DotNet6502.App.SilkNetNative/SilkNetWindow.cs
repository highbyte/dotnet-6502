using AutoMapper;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.NAudioOpenALProvider;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v2;
using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Instrumentation.Stats;
using Highbyte.DotNet6502.Logging;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64;
using Microsoft.Extensions.Logging;
using Silk.NET.SDL;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video;
using AutoMapper.Internal.Mappers;

namespace Highbyte.DotNet6502.App.SilkNetNative;

public enum EmulatorState
{
    Uninitialized,
    Running,
    Paused
}

public class SilkNetWindow
{
    private readonly ILogger _logger;
    private readonly IWindow _window;

    private readonly EmulatorConfig _emulatorConfig;
    public EmulatorConfig EmulatorConfig => _emulatorConfig;

    private readonly SystemList<SilkNetRenderContextContainer, SilkNetInputHandlerContext, NAudioAudioHandlerContext> _systemList;
    public SystemList<SilkNetRenderContextContainer, SilkNetInputHandlerContext, NAudioAudioHandlerContext> SystemList => _systemList;

    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private string _currentSystemName = default!;
    private readonly bool _defaultAudioEnabled;
    private float _defaultAudioVolumePercent;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMapper _mapper;

    public float CanvasScale
    {
        get { return _emulatorConfig.CurrentDrawScale; }
        set { _emulatorConfig.CurrentDrawScale = value; }
    }

    public const int DEFAULT_WIDTH = 1000;
    public const int DEFAULT_HEIGHT = 700;
    public const int DEFAULT_RENDER_HZ = 60;

    public IWindow Window { get { return _window; } }

    public EmulatorState EmulatorState { get; set; } = EmulatorState.Uninitialized;

    private const string HostStatRootName = "SilkNet";
    private const string SystemTimeStatName = "Emulator-SystemTime";
    private const string RenderTimeStatName = "RenderTime";
    private const string InputTimeStatName = "InputTime";
    private const string AudioTimeStatName = "AudioTime";

    private readonly Instrumentations _systemInstrumentations = new();
    private ElapsedMillisecondsTimedStatSystem _systemTime;
    private ElapsedMillisecondsTimedStatSystem _renderTime;
    private ElapsedMillisecondsTimedStatSystem _inputTime;
    //private ElapsedMillisecondsTimedStatSystem _audioTime;

    private readonly PerSecondTimedStat _updateFps = InstrumentationBag.Add<PerSecondTimedStat>($"{HostStatRootName}-OnUpdateFPS");
    private readonly PerSecondTimedStat _renderFps = InstrumentationBag.Add<PerSecondTimedStat>($"{HostStatRootName}-OnRenderFPS");


    // Render context container for SkipSharp (surface/canvas) and SilkNetOpenGl
    private SilkNetRenderContextContainer _silkNetRenderContextContainer = default!;
    // SilkNet input handling
    private SilkNetInputHandlerContext _silkNetInputHandlerContext = default!;
    // NAudio audio handling
    private NAudioAudioHandlerContext _naudioAudioHandlerContext = default!;

    // Emulator    
    private SystemRunner _systemRunner = default!;
    public SystemRunner SystemRunner => _systemRunner!;

    // Monitor
    private SilkNetImGuiMonitor _monitor = default!;
    public SilkNetImGuiMonitor Monitor => _monitor;

    // Instrumentations panel
    private SilkNetImGuiStatsPanel _statsPanel = default!;
    public SilkNetImGuiStatsPanel StatsPanel => _statsPanel;

    // Logs panel
    private SilkNetImGuiLogsPanel _logsPanel = default!;
    public SilkNetImGuiLogsPanel LogsPanel => _logsPanel;

    // Menu
    private SilkNetImGuiMenu _menu = default!;
    private bool _statsWasEnabled = false;
    //private bool _logsWasEnabled = false;

    private readonly List<ISilkNetImGuiWindow> _imGuiWindows = new List<ISilkNetImGuiWindow>();
    private bool _atLeastOneImGuiWindowHasFocus => _imGuiWindows.Any(x => x.Visible && x.WindowIsFocused);

    // GL and other ImGui resources
    private GL _gl = default!;
    private IInputContext _inputContext = default!;
    private ImGuiController _imGuiController = default!;

    public SilkNetWindow(
        EmulatorConfig emulatorConfig,
        IWindow window,
        SystemList<SilkNetRenderContextContainer, SilkNetInputHandlerContext, NAudioAudioHandlerContext> systemList,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        ILoggerFactory loggerFactory,
        IMapper mapper
        )
    {
        _emulatorConfig = emulatorConfig;
        _emulatorConfig.CurrentDrawScale = _emulatorConfig.DefaultDrawScale;
        _window = window;
        _systemList = systemList;
        _logStore = logStore;
        _logConfig = logConfig;
        _defaultAudioEnabled = true;
        _defaultAudioVolumePercent = 20.0f;

        _loggerFactory = loggerFactory;
        _mapper = mapper;
        _logger = loggerFactory.CreateLogger(typeof(SilkNetWindow).Name);
    }

    public void Run()
    {
        _window.Load += OnLoad;
        _window.Closing += OnClosing;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Resize += OnResize;

        _window.Run();
        // Cleanup SilNet window resources
        _window?.Dispose();
    }

    protected void OnLoad()
    {
        SetUninitializedWindow();

        InitRendering();
        InitInput();
        InitAudio();

        _systemList.InitContext(() => _silkNetRenderContextContainer, () => _silkNetInputHandlerContext, () => _naudioAudioHandlerContext);

        InitImGui();

        // Init main menu UI
        _menu = new SilkNetImGuiMenu(this, _emulatorConfig.DefaultEmulator, _defaultAudioEnabled, _defaultAudioVolumePercent, _mapper, _loggerFactory);

        // Create other UI windows
        _statsPanel = CreateStatsUI();
        _monitor = CreateMonitorUI(_statsPanel, _emulatorConfig.Monitor);
        _logsPanel = CreateLogsUI(_logStore, _logConfig);

        // Add all ImGui windows to a list
        _imGuiWindows.Add(_menu);
        _imGuiWindows.Add(_statsPanel);
        _imGuiWindows.Add(_monitor);
        _imGuiWindows.Add(_logsPanel);
    }

    protected void OnClosing()
    {
        // Dispose Monitor/Instrumentations panel
        // _monitor.Cleanup();
        // _statsPanel.Cleanup();
        DestroyImGuiController();

        // Cleanup SkiaSharp resources
        _silkNetRenderContextContainer.Cleanup();

        // Cleanup SilkNet input resources
        _silkNetInputHandlerContext.Cleanup();

        // Cleanup NAudio audio resources
        _naudioAudioHandlerContext.Cleanup();
    }

    /// <summary>
    /// Runs on every Update Frame event.
    /// 
    /// Use this method to run logic.
    /// 
    /// </summary>
    /// <param name=""></param>
    protected void OnUpdate(double deltaTime)
    {
        if (EmulatorState != EmulatorState.Running)
            return;
        _updateFps.Update();
        RunEmulator();
    }

    private void SetUninitializedWindow()
    {
        Window.Size = new Vector2D<int>(DEFAULT_WIDTH, DEFAULT_HEIGHT);
        Window.UpdatesPerSecond = DEFAULT_RENDER_HZ;
        EmulatorState = EmulatorState.Uninitialized;
    }

    private void InitRendering()
    {
        _silkNetRenderContextContainer?.Cleanup();

        // Init SkipSharp resources (must be done in OnLoad, otherwise no OpenGL context will exist create by SilkNet.)
        //_skiaRenderContext = new SkiaRenderContext(s_window.Size.X, s_window.Size.Y, _canvasScale);
        GRGlGetProcedureAddressDelegate getProcAddress = (name) =>
        {
            var addrFound = _window.GLContext!.TryGetProcAddress(name, out var addr);
            return addrFound ? addr : 0;
        };

        var skiaRenderContext = new SkiaRenderContext(
            getProcAddress,
            _window.FramebufferSize.X,
            _window.FramebufferSize.Y,
            _emulatorConfig.CurrentDrawScale * (_window.FramebufferSize.X / _window.Size.X));

        var silkNetOpenGlRenderContext = new SilkNetOpenGlRenderContext(_window, _emulatorConfig.CurrentDrawScale);

        _silkNetRenderContextContainer = new SilkNetRenderContextContainer(skiaRenderContext, silkNetOpenGlRenderContext);
    }

    public void SetCurrentSystem(string systemName)
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            throw new DotNet6502Exception("Internal error. Cannot change system while running");

        _currentSystemName = systemName;

        _logger.LogInformation($"System selected: {_currentSystemName}");

        if (_systemList.IsValidConfig(systemName).Result)
        {
            var system = _systemList.GetSystem(systemName).Result;
            var screen = system.Screen;
            Window.Size = new Vector2D<int>((int)(screen.VisibleWidth * CanvasScale), (int)(screen.VisibleHeight * CanvasScale));
            Window.UpdatesPerSecond = screen.RefreshFrequencyHz;

            InitRendering();
        }
        else
        {
        }
    }

    private void InitInstrumentation(ISystem system)
    {
        _systemInstrumentations.Clear();
        _systemTime = _systemInstrumentations.Add($"{HostStatRootName}-{SystemTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
        _renderTime = _systemInstrumentations.Add($"{HostStatRootName}-{RenderTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
        _inputTime = _systemInstrumentations.Add($"{HostStatRootName}-{InputTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
        //_audioTime = InstrumentationBag.Add($"{HostStatRootName}-{AudioTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
    }

    public void SetVolumePercent(float volumePercent)
    {
        _defaultAudioVolumePercent = volumePercent;
        _naudioAudioHandlerContext.SetMasterVolumePercent(masterVolumePercent: volumePercent);
    }

    public void Start()
    {
        if (EmulatorState == EmulatorState.Running)
            return;

        if (!_systemList.IsValidConfig(_currentSystemName).Result)
            throw new DotNet6502Exception("Internal error. Cannot start emulator if current system config is invalid.");

        // Force a full GC to free up memory, so it won't risk accumulate memory usage if GC has not run for a while.
        var m0 = GC.GetTotalMemory(forceFullCollection: true);
        _logger.LogInformation("Allocated memory before starting emulator: " + m0);

        // Only create a new instance of SystemRunner if we previously has not started (so resume after pause works).
        if (EmulatorState == EmulatorState.Uninitialized)
            _systemRunner = _systemList.BuildSystemRunner(_currentSystemName).Result;

        InitInstrumentation(_systemRunner.System);

        _monitor.Init(_systemRunner!);

        _systemRunner.AudioHandler.StartPlaying();

        EmulatorState = EmulatorState.Running;

        _logger.LogInformation($"System started: {_currentSystemName}");
    }

    public void Pause()
    {
        if (EmulatorState == EmulatorState.Paused || EmulatorState == EmulatorState.Uninitialized)
            return;

        _systemRunner.AudioHandler.PausePlaying();
        EmulatorState = EmulatorState.Paused;

        _logger.LogInformation($"System paused: {_currentSystemName}");
    }

    public void Reset()
    {
        if (EmulatorState == EmulatorState.Uninitialized)
            return;

        if (_statsPanel.Visible)
            ToggleStatsPanel();

        _systemRunner?.Cleanup();
        _systemRunner = default!;
        EmulatorState = EmulatorState.Uninitialized;
        Start();
    }

    public void Stop()
    {
        if (EmulatorState == EmulatorState.Running)
            Pause();

        if (_statsPanel.Visible)
            ToggleStatsPanel();

        _systemRunner.Cleanup();
        _systemRunner = default!;
        SetUninitializedWindow();
        InitRendering();

        _logger.LogInformation($"System stopped: {_currentSystemName}");
    }

    private void RunEmulator()
    {
        // Don't update emulator state when monitor is visible
        if (_monitor.Visible)
            return;

        if (_silkNetInputHandlerContext.Quit || _monitor.Quit)
        {
            _window.Close();
            return;
        }

        // Handle input
        if (!_atLeastOneImGuiWindowHasFocus)
        {
            _inputTime.Start();
            _systemRunner.ProcessInput();
            _inputTime.Stop();
        }

        // Run emulator for one frame worth of emulated CPU cycles 
        ExecEvaluatorTriggerResult execEvaluatorTriggerResult;
        _systemTime.Start();
        execEvaluatorTriggerResult = _systemRunner.RunEmulatorOneFrame();
        _systemTime.Stop();

        // Show monitor if we encounter breakpoint or other break
        if (execEvaluatorTriggerResult.Triggered)
            _monitor.Enable(execEvaluatorTriggerResult);
    }

    /// <summary>
    /// Runs on every Render Frame event.
    /// 
    /// Use this method to render the world.
    /// 
    /// This method is called at a RenderFrequency set in the GameWindowSettings object.
    /// </summary>
    /// <param name="args"></param>
    protected void OnRender(double deltaTime)
    {
        _renderFps.Update();
        RenderEmulator(deltaTime);
    }

    private void RenderEmulator(double deltaTime)
    {
        // Make sure ImGui is up-to-date
        _imGuiController.Update((float)deltaTime);

        var emulatorRendered = false;

        if (EmulatorState == EmulatorState.Running)
        {
            if (_monitor.Visible || _statsPanel.Visible || _logsPanel.Visible)
                _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            _renderTime.Start();
            // Render emulator system screen
            _systemRunner.Draw();
            // Flush the SkiaSharp Context
            _silkNetRenderContextContainer.SkiaRenderContext.GetGRContext().Flush();
            _renderTime.Stop();

            emulatorRendered = true;

            // SilkNet windows are what's known as "double-buffered". In essence, the window manages two buffers.
            // One is rendered to while the other is currently displayed by the window.
            // This avoids screen tearing, a visual artifact that can happen if the buffer is modified while being displayed.
            // After drawing, call this function to swap the buffers. If you don't, it won't display what you've rendered.

            // NOTE: s_window.SwapBuffers() seem to have some problem. Window is darker, and some flickering.
            //       Use windowOptions.ShouldSwapAutomatically = true  instead
            //s_window.SwapBuffers();

            // Render monitor if enabled
            if (_monitor.Visible)
                _monitor.PostOnRender();

            // Render stats if enabled
            if (_statsPanel.Visible)
                _statsPanel.PostOnRender();
        }

        // Render logs if enabled
        if (_logsPanel.Visible)
            _logsPanel.PostOnRender();

        if (!emulatorRendered)
        {
            if (_menu.Visible)
                _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
            // Seems the canvas has to be drawn & flushed for ImGui stuff to be visible on top
            var canvas = _silkNetRenderContextContainer.SkiaRenderContext.GetCanvas();
            canvas.Clear();
            _silkNetRenderContextContainer.SkiaRenderContext.GetGRContext().Flush();
        }

        if (_menu.Visible)
            _menu.PostOnRender();

        // Render any ImGui UI rendered above emulator.
        _imGuiController?.Render();
    }

    private void OnResize(Vector2D<int> vec2)
    {
    }

    private void InitInput()
    {
        _silkNetInputHandlerContext = new SilkNetInputHandlerContext(_window, _loggerFactory);

        _inputContext = _window.CreateInput();
        // Listen to key to enable monitor
        if (_inputContext.Keyboards == null || _inputContext.Keyboards.Count == 0)
            throw new DotNet6502Exception("Keyboard not found");
        var primaryKeyboard = _inputContext.Keyboards[0];

        // Listen to special key that will show/hide overlays for monitor/stats
        primaryKeyboard.KeyDown += OnKeyDown;
    }

    private void InitAudio()
    {
        // Output to NAudio built-in output (Windows only)
        //var wavePlayer = new WaveOutEvent
        //{
        //    NumberOfBuffers = 2,
        //    DesiredLatency = 100,
        //}

        // Output to OpenAL (cross platform) instead of via NAudio built-in output (Windows only)
        var wavePlayer = new SilkNetOpenALWavePlayer()
        {
            NumberOfBuffers = 2,
            DesiredLatency = 40
        };

        _naudioAudioHandlerContext = new NAudioAudioHandlerContext(
            wavePlayer,
            initialVolumePercent: 20);
    }

    private void InitImGui()
    {
        // Init ImGui resource
        _gl = GL.GetApi(_window);
        _imGuiController = new ImGuiController(
            _gl,
            _window, // pass in our window
            _inputContext // input context
        );
    }

    private SilkNetImGuiMonitor CreateMonitorUI(SilkNetImGuiStatsPanel statsPanel, MonitorConfig monitorConfig)
    {
        // Init Monitor ImGui resources 
        var monitor = new SilkNetImGuiMonitor(monitorConfig);
        monitor.MonitorStateChange += (s, monitorEnabled) => _silkNetInputHandlerContext.ListenForKeyboardInput(enabled: !monitorEnabled);
        monitor.MonitorStateChange += (s, monitorEnabled) =>
        {
            if (monitorEnabled)
                statsPanel.Disable();
        };
        return monitor;
    }

    private SilkNetImGuiStatsPanel CreateStatsUI()
    {
        return new SilkNetImGuiStatsPanel(GetStats);
    }

    private List<(string name, IStat stat)> GetStats()
    {
        return InstrumentationBag.Stats
            .Union(_systemInstrumentations.Stats)
            .Union(_systemRunner.System.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{SystemTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.Renderer.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{RenderTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.AudioHandler.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{AudioTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.InputHandler.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{InputTimeStatName}-{x.Name}", x.Stat)))
            .ToList();
    }

    private SilkNetImGuiLogsPanel CreateLogsUI(DotNet6502InMemLogStore logStore, DotNet6502InMemLoggerConfiguration logConfig)
    {
        return new SilkNetImGuiLogsPanel(logStore, logConfig);
    }

    private void DestroyImGuiController()
    {
        _imGuiController?.Dispose();
        _inputContext?.Dispose();
        _gl?.Dispose();
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int x)
    {
        if (key == Key.F6)
            ToggleMainMenu();
        if (key == Key.F10)
            ToggleLogsPanel();

        if (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused)
        {
            if (key == Key.F11)
                ToggleStatsPanel();
            if (key == Key.F12)
                ToggleMonitor();
        }
    }

    public void ToggleMainMenu()
    {
        if (_menu.Visible)
            _menu.Disable();
        else
            _menu.Enable();
    }

    public void ToggleStatsPanel()
    {
        if (_monitor.Visible)
            return;

        if (_statsPanel.Visible)
        {
            _statsPanel.Disable();
            _systemRunner.System.InstrumentationEnabled = false;
            _statsWasEnabled = false;
        }
        else
        {
            _systemRunner.System.InstrumentationEnabled = true;
            _statsPanel.Enable();
        }
    }

    public void ToggleLogsPanel()
    {
        if (_monitor.Visible)
            return;

        if (_logsPanel.Visible)
        {
            //_logsWasEnabled = true;
            _logsPanel.Disable();
        }
        else
        {
            _logsPanel.Enable();
        }
    }

    public void ToggleMonitor()
    {
        if (_statsPanel.Visible)
        {
            _statsWasEnabled = true;
            _statsPanel.Disable();
        }

        if (_monitor.Visible)
        {
            _monitor.Disable();
            if (_statsWasEnabled)
            {
                _systemRunner.System.InstrumentationEnabled = true;
                _statsPanel.Enable();
            }
        }
        else
        {
            _monitor.Enable();
        }
    }
}
