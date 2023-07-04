using Highbyte.DotNet6502.App.SkiaNative.Instrumentation.Stats;
using Highbyte.DotNet6502.App.SkiaNative.Stats;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
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

    private readonly SystemList<SkiaRenderContext, SilkNetInputHandlerContext, NullAudioHandlerContext> _systemList;
    public SystemList<SkiaRenderContext, SilkNetInputHandlerContext, NullAudioHandlerContext> SystemList => _systemList;

    private float _canvasScale;
    private readonly string _defaultSystemName;
    private string _currentSystemName;

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
    private readonly ElapsedMillisecondsTimedStat _renderTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("SkiaSharp-RenderTime");
    private readonly PerSecondTimedStat _updateFps = InstrumentationBag.Add<PerSecondTimedStat>("SilkNetSkiaSharp-OnUpdateFPS");
    private readonly PerSecondTimedStat _renderFps = InstrumentationBag.Add<PerSecondTimedStat>("SilkNetSkiaSharp-OnRenderFPS");

    // SkipSharp context/surface/canvas
    private SkiaRenderContext _skiaRenderContext;
    // SilkNet input handling
    private SilkNetInputHandlerContext _silkNetInputHandlerContext;

    // Emulator    
    private SystemRunner _systemRunner;

    // Monitor
    private SilkNetImGuiMonitor _monitor;
    public SilkNetImGuiMonitor Monitor => _monitor;

    // Stats panel
    private SilkNetImGuiStatsPanel _statsPanel;
    public SilkNetImGuiStatsPanel StatsPanel => _statsPanel;

    // Menu
    private SilkNetImGuiMenu _menu;
    private bool _statsWasEnabled = false;

    // GL and other ImGui resources
    private GL _gl;
    private IInputContext _inputContext;
    private ImGuiController _imGuiController;


    public SilkNetWindow(
        MonitorConfig monitorConfig,
        IWindow window,
        SystemList<SkiaRenderContext, SilkNetInputHandlerContext, NullAudioHandlerContext> systemList,
        float scale,
        string defaultSystemName)
    {
        _monitorConfig = monitorConfig;
        _window = window;
        _systemList = systemList;
        _canvasScale = scale;
        _defaultSystemName = defaultSystemName;
    }

    public void Run()
    {
        _window.Load += OnLoad;
        _window.Closing += OnClosing;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Resize += OnResize;

        _window.Run();
    }

    protected void OnLoad()
    {
        SetUninitializedWindow();

        InitRendering();
        InitInput();

        _systemList.InitContext(() => _skiaRenderContext, () => _silkNetInputHandlerContext, () => new NullAudioHandlerContext());

        InitImGui();

        // Init main menu
        _menu = new SilkNetImGuiMenu(this, _defaultSystemName);

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

        // Cleanup SilNet window resources
        _window?.Dispose();
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
            var screen = (IScreen)system;
            Window.Size = new Vector2D<int>((int)(screen.VisibleWidth * _canvasScale), (int)(screen.VisibleHeight * _canvasScale));
            Window.UpdatesPerSecond = screen.RefreshFrequencyHz;

            InitRendering();
        }
        else
        {

        }
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

        InitMonitorAndStats();

        EmulatorState = EmulatorState.Running;
    }

    public void Pause()
    {
        if (EmulatorState == EmulatorState.Paused || EmulatorState == EmulatorState.Uninitialized)
            return;
        EmulatorState = EmulatorState.Paused;
    }

    public void Reset()
    {
        if (EmulatorState == EmulatorState.Uninitialized)
            return;
        Stop();
        //SetCurrentSystem(_selectedSystemName);
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

        // Run emulator.
        // Handle input
        using (_inputTime.Measure())
        {
            _systemRunner.ProcessInput();
        }

        // Run emulator for one frame worth of emulated CPU cycles 
        bool cont;
        using (_systemTime.Measure())
        {
            cont = _systemRunner.RunEmulatorOneFrame(out Dictionary<string, double> detailedStats);
        }

        // Show monitor if we encounter breakpoint or other break
        if (!cont)
            _monitor.Enable();
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
            if (_monitor.Visible || _statsPanel.Visible)
            {
                _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
            }

            using (_renderTime.Measure())
            {
                // Render emulator system screen
                _systemRunner.Draw();

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

    private void InitMonitorAndStats()
    {
        // Init Monitor ImGui resources 
        _monitor = new SilkNetImGuiMonitor(_systemRunner, _monitorConfig);
        _monitor.MonitorStateChange += (s, monitorEnabled) => _silkNetInputHandlerContext.ListenForKeyboardInput(enabled: !monitorEnabled);
        _monitor.MonitorStateChange += (s, monitorEnabled) =>
        {
            if (monitorEnabled)
                _statsPanel.Disable();
        };

        // Init StatsPanel ImGui resources
        _statsPanel = new SilkNetImGuiStatsPanel();
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
            _menu.Visible = !_menu.Visible;
        }

        if (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused)
        {
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
