using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.NAudioOpenALProvider;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SilkNetNative;

public class SilkNetHostApp : HostApp<SilkNetRenderContextContainer, SilkNetInputHandlerContext, NAudioAudioHandlerContext>
{
    // --------------------
    // Injected variables
    // --------------------
    private readonly ILogger _logger;
    private readonly IWindow _window;
    private readonly EmulatorConfig _emulatorConfig;
    public EmulatorConfig EmulatorConfig => _emulatorConfig;

    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private readonly bool _defaultAudioEnabled;
    private float _defaultAudioVolumePercent;
    private readonly ILoggerFactory _loggerFactory;

    // --------------------
    // Other variables / constants
    // --------------------
    private SilkNetRenderContextContainer _renderContextContainer = default!;
    private SilkNetInputHandlerContext _inputHandlerContext = default!;
    private NAudioAudioHandlerContext _audioHandlerContext = default!;

    public float CanvasScale
    {
        get { return _emulatorConfig.CurrentDrawScale; }
        set { _emulatorConfig.CurrentDrawScale = value; }
    }
    public const int DEFAULT_WIDTH = 1000;
    public const int DEFAULT_HEIGHT = 700;
    public const int DEFAULT_RENDER_HZ = 60;

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
    private ImGuiController _imGuiController = default!;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemList"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="window"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    public SilkNetHostApp(
        SystemList<SilkNetRenderContextContainer, SilkNetInputHandlerContext, NAudioAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory,
        EmulatorConfig emulatorConfig,
        IWindow window,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig

        ) : base("SilkNet", systemList, loggerFactory)
    {
        _emulatorConfig = emulatorConfig;
        _emulatorConfig.CurrentDrawScale = _emulatorConfig.DefaultDrawScale;
        _window = window;
        _logStore = logStore;
        _logConfig = logConfig;
        _defaultAudioEnabled = true;
        _defaultAudioVolumePercent = 20.0f;

        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(SilkNetHostApp).Name);
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

        _renderContextContainer = CreateRenderContext();
        _inputHandlerContext = CreateInputHandlerContext();
        _audioHandlerContext = CreateAudioHandlerContext();

        base.SetContexts(() => _renderContextContainer, () => _inputHandlerContext, () => _audioHandlerContext);
        base.InitRenderContext();
        base.InitInputHandlerContext();
        base.InitAudioHandlerContext();

        ConfigureSilkNetInput();

        InitImGui();

        // Init main menu UI
        _menu = new SilkNetImGuiMenu(this, _emulatorConfig.DefaultEmulator, _defaultAudioEnabled, _defaultAudioVolumePercent, _loggerFactory);

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
        base.Close();
    }

    public override void OnAfterSelectSystem()
    {
    }

    public override bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        // Force a full GC to free up memory, so it won't risk accumulate memory usage if GC has not run for a while.
        var m0 = GC.GetTotalMemory(forceFullCollection: true);
        _logger.LogInformation("Allocated memory before starting emulator: " + m0);

        // Make sure to adjust window size and render frequency to match the system that is about to be started
        if (EmulatorState == EmulatorState.Uninitialized)
        {
            var screen = systemAboutToBeStarted.Screen;
            _window.Size = new Vector2D<int>((int)(screen.VisibleWidth * CanvasScale), (int)(screen.VisibleHeight * CanvasScale));
            _window.UpdatesPerSecond = screen.RefreshFrequencyHz;

            _renderContextContainer?.Cleanup();
            _renderContextContainer = CreateRenderContext();
            base.InitRenderContext();
        }
        return true;
    }

    public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
    {
        // Init monitor for current system started if this system was not started before
        if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
            _monitor.Init(CurrentSystemRunner!);
    }

    public override void OnAfterClose()
    {
        // Dispose Monitor/Instrumentations panel
        //_monitor.Cleanup();
        //_statsPanel.Cleanup();
        DestroyImGuiController();

        // Cleanup contexts
        _renderContextContainer?.Cleanup();
        _inputHandlerContext?.Cleanup();
        _audioHandlerContext?.Cleanup();
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
        base.RunEmulatorOneFrame();
    }

    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = false;
        shouldReceiveInput = false;
        // Don't update emulator state when monitor is visible
        if (_monitor.Visible)
            return;
        // Don't update emulator state when app is quiting
        if (_inputHandlerContext.Quit || _monitor.Quit)
        {
            _window.Close();
            return;
        }

        shouldRun = true;

        // Only receive input to emulator when no ImGui window has focus
        if (!_atLeastOneImGuiWindowHasFocus)
            shouldReceiveInput = true;
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        // Show monitor if we encounter breakpoint or other break
        if (execEvaluatorTriggerResult.Triggered)
            _monitor.Enable(execEvaluatorTriggerResult);
    }


    /// <summary>
    /// Runs on every Render Frame event. Draws one emulator frame on screen.
    /// 
    /// This method is called at a RenderFrequency set in the GameWindowSettings object.
    /// </summary>
    /// <param name="args"></param>
    protected void OnRender(double deltaTime)
    {
        //RenderEmulator(deltaTime);

        // Make sure ImGui is up-to-date
        _imGuiController.Update((float)deltaTime);

        // Draw emulator on screen
        base.DrawFrame();
    }

    public override void OnBeforeDrawFrame(bool emulatorWillBeRendered)
    {
        // If any ImGui window is visible, make sure to clear Gl buffer before rendering emulator
        if (emulatorWillBeRendered)
        {
            if (_monitor.Visible || _statsPanel.Visible || _logsPanel.Visible)
                _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        }
    }
    public override void OnAfterDrawFrame(bool emulatorRendered)
    {
        if (emulatorRendered)
        {
            // Flush the SkiaSharp Context
            _renderContextContainer.SkiaRenderContext.GetGRContext().Flush();

            // Render monitor if enabled and emulator was rendered
            if (_monitor.Visible)
                _monitor.PostOnRender();

            // Render stats if enabled and emulator was rendered
            if (_statsPanel.Visible)
                _statsPanel.PostOnRender();
        }

        // Render logs if enabled, regardless of if emulator was rendered or not
        if (_logsPanel.Visible)
            _logsPanel.PostOnRender();

        // If emulator was not rendered, clear Gl buffer before rendering ImGui windows
        if (!emulatorRendered)
        {
            if (_menu.Visible)
                _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
            // Seems the canvas has to be drawn & flushed for ImGui stuff to be visible on top
            var canvas = _renderContextContainer.SkiaRenderContext.GetCanvas();
            canvas.Clear();
            _renderContextContainer.SkiaRenderContext.GetGRContext().Flush();
        }

        if (_menu.Visible)
            _menu.PostOnRender();

        // Render any ImGui UI rendered above emulator.
        _imGuiController?.Render();
    }

    private void OnResize(Vector2D<int> vec2)
    {
    }


    private SilkNetRenderContextContainer CreateRenderContext()
    {
        // Init SkipSharp resources (must be done in OnLoad, otherwise no OpenGL context will exist created by SilkNet.)
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

        var renderContextContainer = new SilkNetRenderContextContainer(skiaRenderContext, silkNetOpenGlRenderContext);
        return renderContextContainer;
    }

    private SilkNetInputHandlerContext CreateInputHandlerContext()
    {
        var inputHandlerContext = new SilkNetInputHandlerContext(_window, _loggerFactory);
        return inputHandlerContext;
    }

    private NAudioAudioHandlerContext CreateAudioHandlerContext()
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

        return new NAudioAudioHandlerContext(
            wavePlayer,
            initialVolumePercent: 20);
    }

    private void ConfigureSilkNetInput()
    {
        // Listen to keys to enable monitor, logs, stats, etc.
        var inputContext = _inputHandlerContext.InputContext;
        if (inputContext.Keyboards == null || inputContext.Keyboards.Count == 0)
            throw new DotNet6502Exception("Keyboard not found");
        var primaryKeyboard = inputContext.Keyboards[0];

        // Listen to special key that will show/hide overlays for monitor/stats
        primaryKeyboard.KeyDown += OnKeyDown;
    }

    public void SetVolumePercent(float volumePercent)
    {
        _defaultAudioVolumePercent = volumePercent;
        _audioHandlerContext.SetMasterVolumePercent(masterVolumePercent: volumePercent);
    }

    private void SetUninitializedWindow()
    {
        _window.Size = new Vector2D<int>(DEFAULT_WIDTH, DEFAULT_HEIGHT);
        _window.UpdatesPerSecond = DEFAULT_RENDER_HZ;
    }

    private void InitImGui()
    {
        // Init ImGui resource
        _gl = GL.GetApi(_window);
        _imGuiController = new ImGuiController(
            _gl,
            _window, // pass in our window
            _inputHandlerContext.InputContext // input context
        );
    }

    private SilkNetImGuiMonitor CreateMonitorUI(SilkNetImGuiStatsPanel statsPanel, MonitorConfig monitorConfig)
    {
        // Init Monitor ImGui resources 
        var monitor = new SilkNetImGuiMonitor(monitorConfig);
        monitor.MonitorStateChange += (s, monitorEnabled) => _inputHandlerContext.ListenForKeyboardInput(enabled: !monitorEnabled);
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

    private SilkNetImGuiLogsPanel CreateLogsUI(DotNet6502InMemLogStore logStore, DotNet6502InMemLoggerConfiguration logConfig)
    {
        return new SilkNetImGuiLogsPanel(logStore, logConfig);
    }

    private void DestroyImGuiController()
    {
        _imGuiController?.Dispose();
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
    private void ToggleMainMenu()
    {
        if (_menu.Visible)
            _menu.Disable();
        else
            _menu.Enable();
    }


    public float Scale
    {
        get { return _emulatorConfig.CurrentDrawScale; }
        set { _emulatorConfig.CurrentDrawScale = value; }
    }

    public void ToggleMonitor()
    {
        // Only be able to toggle monitor if emulator is running or paused
        if (EmulatorState == EmulatorState.Uninitialized)
            return;

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
                CurrentRunningSystem!.InstrumentationEnabled = true;
                _statsPanel.Enable();
            }
        }
        else
        {
            _monitor.Enable();
        }
    }

    public void ToggleStatsPanel()
    {
        // Only be able to toggle stats if emulator is running or paused
        if (EmulatorState == EmulatorState.Uninitialized)
            return;

        if (_monitor.Visible)
            return;

        if (_statsPanel.Visible)
        {
            _statsPanel.Disable();
            CurrentRunningSystem!.InstrumentationEnabled = false;
            _statsWasEnabled = false;
        }
        else
        {
            CurrentRunningSystem!.InstrumentationEnabled = true;
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

}
