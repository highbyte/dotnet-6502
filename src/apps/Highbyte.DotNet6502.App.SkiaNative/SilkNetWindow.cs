using Highbyte.DotNet6502.App.SkiaNative.Instrumentation.Stats;
using Highbyte.DotNet6502.App.SkiaNative.Stats;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.NAudioOpenALProvider;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Logging;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SkiaNative;

public enum EmulatorState
{
    Uninitialized,
    Running,
    Paused
}

public class SilkNetWindow
{
    private readonly MonitorConfig _monitorConfig;
    private readonly IWindow _window;

    private readonly SystemList<SkiaRenderContext, SilkNetInputHandlerContext, NAudioAudioHandlerContext> _systemList;
    public SystemList<SkiaRenderContext, SilkNetInputHandlerContext, NAudioAudioHandlerContext> SystemList => _systemList;

    private float _canvasScale;
    private readonly string _defaultSystemName;
    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private string _currentSystemName;
    private readonly bool _defaultAudioEnabled;
    private float _defaultAudioVolumePercent;

    public float CanvasScale
    {
        get { return _canvasScale; }
        set { _canvasScale = value; }
    }

    public const int DEFAULT_WIDTH = 1000;
    public const int DEFAULT_HEIGHT = 700;
    public const int DEFAULT_RENDER_HZ = 60;

    public IWindow Window { get { return _window; } }

    public EmulatorState EmulatorState { get; set; } = EmulatorState.Uninitialized;

    private readonly ElapsedMillisecondsTimedStat _inputTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("SilkNet-InputTime");
    private readonly ElapsedMillisecondsTimedStat _systemTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("Emulator-SystemTime");
    private readonly ElapsedMillisecondsStat _systemTimeAudio = InstrumentationBag.Add<ElapsedMillisecondsStat>("Emulator-SystemTime-Audio"); // Detailed part of system time
    private readonly ElapsedMillisecondsTimedStat _renderTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("SkiaSharp-RenderTime");
    private readonly PerSecondTimedStat _updateFps = InstrumentationBag.Add<PerSecondTimedStat>("SilkNetSkiaSharp-OnUpdateFPS");
    private readonly PerSecondTimedStat _renderFps = InstrumentationBag.Add<PerSecondTimedStat>("SilkNetSkiaSharp-OnRenderFPS");

    // SkipSharp context/surface/canvas
    private SkiaRenderContext _skiaRenderContext;
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
    private bool _atLeastOneImGuiWindowHasFocus => _imGuiWindows.Any(x => x.WindowIsFocused);

    // GL and other ImGui resources
    private GL _gl;
    private IInputContext _inputContext;
    private ImGuiController _imGuiController;


    public SilkNetWindow(
        MonitorConfig monitorConfig,
        IWindow window,
        SystemList<SkiaRenderContext, SilkNetInputHandlerContext, NAudioAudioHandlerContext> systemList,
        float scale,
        string defaultSystemName,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig)
    {
        _monitorConfig = monitorConfig;
        _window = window;
        _systemList = systemList;
        _canvasScale = scale;
        _defaultSystemName = defaultSystemName;
        _logStore = logStore;
        _logConfig = logConfig;
        _defaultAudioEnabled = true;
        _defaultAudioVolumePercent = 20.0f;
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

        _systemList.InitContext(() => _skiaRenderContext, () => _silkNetInputHandlerContext, () => _naudioAudioHandlerContext);

        InitImGui();

        // Init main menu UI
        _menu = new SilkNetImGuiMenu(this, _defaultSystemName, _defaultAudioEnabled, _defaultAudioVolumePercent);

        // Create other UI windows
        _statsPanel = CreateStatsUI();
        _monitor = CreateMonitorUI(_statsPanel, _monitorConfig);
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

        // Cleanup Skia resources
        _skiaRenderContext.Cleanup();

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
        GRGlGetProcedureAddressDelegate getProcAddress = (string name) =>
        {
            bool addrFound = _window.GLContext!.TryGetProcAddress(name, out var addr);
            return addrFound ? addr : 0;
        };

        _skiaRenderContext = new SkiaRenderContext(
            getProcAddress,
            _window.FramebufferSize.X,
            _window.FramebufferSize.Y,
            _canvasScale * (_window.FramebufferSize.X / _window.Size.X));
    }

    public void SetCurrentSystem(string systemName)
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            throw new Exception("Internal error. Cannot change system while running");

        _currentSystemName = systemName;

        if (_systemList.IsValidConfig(systemName).Result)
        {
            var system = _systemList.GetSystem(systemName).Result;
            var screen = system.Screen;
            Window.Size = new Vector2D<int>((int)(screen.VisibleWidth * _canvasScale), (int)(screen.VisibleHeight * _canvasScale));
            Window.UpdatesPerSecond = screen.RefreshFrequencyHz;

            InitRendering();
        }
        else
        {

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

        _monitor.Init(_systemRunner!);

        _systemRunner!.AudioHandler.StartPlaying();

        EmulatorState = EmulatorState.Running;
    }

    public void Pause()
    {
        if (EmulatorState == EmulatorState.Paused || EmulatorState == EmulatorState.Uninitialized)
            return;

        _systemRunner!.AudioHandler.PausePlaying();
        EmulatorState = EmulatorState.Paused;
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
    }

    private void RunEmulator()
    {
        // Don't update emulator state when monitor is visible
        if (_monitor.Visible)
        {
            return;
        }

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
            execEvaluatorTriggerResult = _systemRunner.RunEmulatorOneFrame(out Dictionary<string, double> detailedStats);

            if (detailedStats.ContainsKey("Audio"))
            {
                _systemTimeAudio.Set(detailedStats["Audio"]);
                _systemTimeAudio.UpdateStat();
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

        bool emulatorRendered = false;

        if (EmulatorState == EmulatorState.Running)
        {
            if (_monitor.Visible || _statsPanel.Visible || _logsPanel.Visible)
            {
                _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
            }

            using (_renderTime.Measure())
            {
                // Render emulator system screen
                _systemRunner!.Draw();

                // Flush the Skia Context
                _skiaRenderContext.GetGRContext().Flush();
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
            {
                _monitor.PostOnRender();
            }

            // Render stats if enabled
            if (_statsPanel.Visible)
            {
                _statsPanel.PostOnRender();
            }

            // Render logs if enabled
            if (_logsPanel.Visible)
            {
                _logsPanel.PostOnRender();
            }

        }

        if (!emulatorRendered)
        {
            if (_menu.Visible)
            {
                _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
            }
            // Seems the canvas has to be drawn & flushed for ImGui stuff to be visible on top
            var canvas = _skiaRenderContext.GetCanvas();
            canvas.Clear();
            _skiaRenderContext.GetGRContext().Flush();
        }

        if (_menu.Visible)
        {
            _menu.PostOnRender();
        }

        // Render any ImGui UI rendered above emulator.
        _imGuiController?.Render();
    }

    private void OnResize(Vector2D<int> vec2)
    {
    }

    private void InitInput()
    {
        _silkNetInputHandlerContext = new SilkNetInputHandlerContext(_window);

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
        {
            ToggleMainMenu();
        }

        if (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused)
        {
            if (key == Key.F10)
            {
                ToggleLogsPanel();
            }
            if (key == Key.F11)
            {
                ToggleStatsPanel();
            }
            if (key == Key.F12)
            {
                ToggleMonitor();
            }
        }
    }

    public void ToggleMainMenu()
    {
        if (_menu.Visible)
        {
            _menu.Disable();
        }
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