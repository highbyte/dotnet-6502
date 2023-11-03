using System.Collections.Generic;
using AutoMapper;
using Highbyte.DotNet6502.App.SilkNetNative;
using Highbyte.DotNet6502.App.SilkNetNative.Instrumentation.Stats;
using Highbyte.DotNet6502.App.SilkNetNative.Stats;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.NAudioOpenALProvider;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Logging;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;

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
    private string _currentSystemName;
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

    private readonly ElapsedMillisecondsTimedStat _inputTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("SilkNet-InputTime");
    private readonly ElapsedMillisecondsTimedStat _systemTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("Emulator-SystemTime");
    private readonly ElapsedMillisecondsStat _systemTimeAudio = InstrumentationBag.Add<ElapsedMillisecondsStat>("Emulator-SystemTime-Audio"); // Detailed part of system time
    private readonly ElapsedMillisecondsTimedStat _renderTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("SilkNet-RenderTime");
    private readonly PerSecondTimedStat _updateFps = InstrumentationBag.Add<PerSecondTimedStat>("SilkNet-OnUpdateFPS");
    private readonly PerSecondTimedStat _renderFps = InstrumentationBag.Add<PerSecondTimedStat>("SilkNet-OnRenderFPS");

    private const string CustomSystemStatNamePrefix = "Emulator-SystemTime-Custom-";
    private const string CustomRenderStatNamePrefix = "SilkNet-RenderTime-Custom-";
    private Dictionary<string, ElapsedMillisecondsStat> _customStats = new();

    // Render context container for SkipSharp (surface/canvas) and SilkNetOpenGl
    private SilkNetRenderContextContainer _silkNetRenderContextContainer;
    // SilkNet input handling
    private SilkNetInputHandlerContext _silkNetInputHandlerContext;
    // NAudio audio handling
    private NAudioAudioHandlerContext _naudioAudioHandlerContext;

    // Emulator    
    private SystemRunner? _systemRunner;
    public SystemRunner SystemRunner => _systemRunner!;

    // Monitor
    private SilkNetImGuiMonitor _monitor;
    public SilkNetImGuiMonitor Monitor => _monitor;

    // Stats panel
    private SilkNetImGuiStatsPanel _statsPanel;
    public SilkNetImGuiStatsPanel StatsPanel => _statsPanel;

    // Logs panel
    private SilkNetImGuiLogsPanel _logsPanel;
    public SilkNetImGuiLogsPanel LogsPanel => _logsPanel;

    // Menu
    private SilkNetImGuiMenu _menu;
    private bool _statsWasEnabled = false;
    private bool _logsWasEnabled = false;

    readonly List<ISilkNetImGuiWindow> _imGuiWindows = new List<ISilkNetImGuiWindow>();
    private bool _atLeastOneImGuiWindowHasFocus => _imGuiWindows.Any(x => x.Visible && x.WindowIsFocused);

    // GL and other ImGui resources
    private GL _gl;
    private IInputContext _inputContext;
    private ImGuiController _imGuiController;


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
        // Dispose Monitor/Stats panel
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
            throw new Exception("Internal error. Cannot change system while running");

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

    private void InitCustomSystemStats()
    {
        // Remove any existing custom system stats
        foreach (var existingCustomStatName in _customStats.Keys)
        {
            if (existingCustomStatName.StartsWith(CustomSystemStatNamePrefix)
                || existingCustomStatName.StartsWith(CustomRenderStatNamePrefix))
            {
                InstrumentationBag.Remove(existingCustomStatName);
                _customStats.Remove(existingCustomStatName);
            }
        }
        // Add any custom system stats for selected system
        var system = _systemRunner.System;
        foreach (var customStatName in system.DetailedStatNames)
        {
            _customStats.Add($"{CustomSystemStatNamePrefix}{customStatName}", InstrumentationBag.Add<ElapsedMillisecondsStat>($"{CustomSystemStatNamePrefix}{customStatName}"));
        }

        // Add any custom system stats for selected renderer
        var renderer = _systemRunner.Renderer;
        foreach (var customStatName in renderer.DetailedStatNames)
        {
            _customStats.Add($"{CustomRenderStatNamePrefix}{customStatName}", InstrumentationBag.Add<ElapsedMillisecondsStat>($"{CustomRenderStatNamePrefix}{customStatName}"));
        }
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
            throw new Exception("Internal error. Cannot start emulator if current system config is invalid.");

        // Only create a new instance of SystemRunner if we previously has not started (so resume after pause works).
        if (EmulatorState == EmulatorState.Uninitialized)
            _systemRunner = _systemList.BuildSystemRunner(_currentSystemName).Result;

        InitCustomSystemStats();

        _monitor.Init(_systemRunner!);

        _systemRunner!.AudioHandler.StartPlaying();

        EmulatorState = EmulatorState.Running;

        _logger.LogInformation($"System started: {_currentSystemName}");

    }

    public void Pause()
    {
        if (EmulatorState == EmulatorState.Paused || EmulatorState == EmulatorState.Uninitialized)
            return;

        _systemRunner!.AudioHandler.PausePlaying();
        EmulatorState = EmulatorState.Paused;

        _logger.LogInformation($"System paused: {_currentSystemName}");
    }

    public void Reset()
    {
        if (EmulatorState == EmulatorState.Uninitialized)
            return;
        if (EmulatorState == EmulatorState.Running)
            Pause();
        _systemRunner = null;
        EmulatorState = EmulatorState.Uninitialized;
        Start();
    }

    public void Stop()
    {
        if (EmulatorState == EmulatorState.Running)
            Pause();
        _systemRunner = null;
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
            using (_inputTime.Measure())
            {
                _systemRunner!.ProcessInput();
            }
        }

        // Run emulator for one frame worth of emulated CPU cycles 
        ExecEvaluatorTriggerResult execEvaluatorTriggerResult;
        using (_systemTime.Measure())
        {
            execEvaluatorTriggerResult = _systemRunner.RunEmulatorOneFrame(out var detailedStats);

            if (detailedStats.ContainsKey("Audio"))
            {
                _systemTimeAudio.Set(detailedStats["Audio"]);
                _systemTimeAudio.UpdateStat();
            }

            // Update custom system stats
            // TODO: Make custom system stats less messy?
            foreach (var detailedStatName in detailedStats.Keys)
            {
                var statLookup = _customStats.Keys.SingleOrDefault(x => x.EndsWith(detailedStatName));
                if (statLookup != null)
                {
                    _customStats[$"{CustomSystemStatNamePrefix}{detailedStatName}"].Set(detailedStats[detailedStatName]);
                    _customStats[$"{CustomSystemStatNamePrefix}{detailedStatName}"].UpdateStat();
                }
            }
        }

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

            using (_renderTime.Measure())
            {
                // Render emulator system screen
                _systemRunner!.Draw(out var detailedStats);

                // Flush the SkiaSharp Context
                _silkNetRenderContextContainer.SkiaRenderContext.GetGRContext().Flush();

                // Update custom system stats
                // TODO: Make custom system stats less messy?
                foreach (var detailedStatName in detailedStats.Keys)
                {
                    var statLookup = _customStats.Keys.SingleOrDefault(x => x.EndsWith(detailedStatName));
                    if (statLookup != null)
                    {
                        _customStats[$"{CustomRenderStatNamePrefix}{detailedStatName}"].Set(detailedStats[detailedStatName]);
                        _customStats[$"{CustomRenderStatNamePrefix}{detailedStatName}"].UpdateStat();
                    }
                }

            }
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
            throw new Exception("Keyboard not found");
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
        return new SilkNetImGuiStatsPanel();
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
        {
            _menu.Enable();
        }
    }

    public void ToggleStatsPanel()
    {
        if (_monitor.Visible)
            return;

        if (_statsPanel.Visible)
        {
            _statsPanel.Disable();
            _statsWasEnabled = false;
        }
        else
        {
            _statsPanel.Enable();
        }
    }

    public void ToggleLogsPanel()
    {
        if (_monitor.Visible)
            return;

        if (_logsPanel.Visible)
        {
            _logsPanel.Disable();
            _logsWasEnabled = false;
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
                _statsPanel.Enable();
        }
        else
        {
            _monitor.Enable();
        }

    }
}
