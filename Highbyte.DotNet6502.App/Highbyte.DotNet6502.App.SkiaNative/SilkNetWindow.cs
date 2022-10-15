using Highbyte.DotNet6502.App.SkiaNative.Instrumentation.Stats;
using Highbyte.DotNet6502.App.SkiaNative.Stats;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Highbyte.DotNet6502.App.SkiaNative;
public class SilkNetWindow<TSystem>
    where TSystem : ISystem
{
    private readonly MonitorOptions _monitorOptions;
    private static IWindow s_window;
    private readonly Func<SkiaRenderContext, SilkNetInputHandlerContext, SystemRunner> _getSystemRunner;
    private readonly float _canvasScale;

    private readonly ElapsedMillisecondsTimedStat _inputTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("SilkNet-InputTime");
    private readonly ElapsedMillisecondsTimedStat _systemTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("Emulator-SystemTime");
    private readonly ElapsedMillisecondsTimedStat _renderTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("SkiaSharp-RenderTime");
    private readonly PerSecondTimedStat _fps = InstrumentationBag.Add<PerSecondTimedStat>("SilkNetSkiaSharp-OnRenderFPS");

    // SkipSharp context/surface/canvas
    private SkiaRenderContext _skiaRenderContext;
    // SilkNet input handling
    private SilkNetInputHandlerContext _silkNetInputHandlerContext;

    // Emulator    
    private SystemRunner _systemRunner;

    // Monitor
    private SilkNetImgUIMonitor _monitor;

    // Stats panel
    private SilkNetImGuiStatsPanel _statsPanel;

    // GL and other ImGui resources
    private GL _gl;
    private IInputContext _inputContext;
    private ImGuiController _imGuiController;
    private bool _statsWasEnabled = false;


    public SilkNetWindow(
        MonitorOptions monitorOptions,
        IWindow window,
        Func<SkiaRenderContext, SilkNetInputHandlerContext, SystemRunner> getSystemRunner,
        float scale = 1.0f)
    {
        _monitorOptions = monitorOptions;
        s_window = window;
        _getSystemRunner = getSystemRunner;
        _canvasScale = scale;
    }

    public void Run()
    {
        s_window.Load += OnLoad;
        s_window.Closing += OnClosing;
        s_window.Update += OnUpdate;
        s_window.Render += OnRender;
        s_window.Resize += OnResize;

        s_window.Run();
    }

    protected void OnLoad()
    {
        // Init SkipSharp resources (must be done in OnLoad, otherwise no OpenGL context will exist create by SilkNet.)
        _skiaRenderContext = new SkiaRenderContext(s_window.Size.X, s_window.Size.Y, _canvasScale);
        _silkNetInputHandlerContext = new SilkNetInputHandlerContext(s_window);
        _systemRunner = _getSystemRunner(_skiaRenderContext, _silkNetInputHandlerContext);

        InitImGui();
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
        s_window?.Dispose();
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
        // Don't update emulator state when monitor is visible
        if (_monitor.Visible)
        {
            return;
        }

        if (_silkNetInputHandlerContext.Quit || _monitor.Quit)
        {
            s_window.Close();
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
            cont = _systemRunner.RunEmulatorOneFrame();
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
        _fps.Update();

        if (_monitor.Visible || _statsPanel.Visible)
        {
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        }

        using (_renderTime.Measure())
        {
            // Render emulator system screen
            _systemRunner.Draw();

            // Flush the Skia Context
            _skiaRenderContext.GRContext?.Flush();
        }

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
            _monitor.PostOnRender(_imGuiController, deltaTime);
        }

        // Render stats if enabled
        if (_statsPanel.Visible)
        {
            _statsPanel.PostOnRender(_imGuiController, deltaTime);
        }

    }

    private void OnResize(Vector2D<int> vec2)
    {
    }

    private void InitImGui()
    {
        // Init ImGui resource
        _gl = GL.GetApi(s_window);
        _inputContext = s_window.CreateInput();
        // Listen to key to enable monitor
        if (_inputContext.Keyboards == null || _inputContext.Keyboards.Count == 0)
            throw new Exception("Keyboard not found");
        var primaryKeyboard = _inputContext.Keyboards[0];

        // Listen to special key that will show/hide overlays for monitor/stats
        primaryKeyboard.KeyDown += OnKeyDown;

        // Init Monitor ImGui resources 
        _monitor = new SilkNetImgUIMonitor(_systemRunner, _monitorOptions);
        _monitor.MonitorStateChange += (s, monitorEnabled) => _silkNetInputHandlerContext.ListenForKeyboardInput(enabled: !monitorEnabled);
        _monitor.MonitorStateChange += (s, monitorEnabled) =>
        {
            if (monitorEnabled)
                _statsPanel.Disable();
        };

        // Init StatsPanel ImGui resources
        _statsPanel = new SilkNetImGuiStatsPanel(_systemRunner);

        CreateImGuiController();
    }

    private void CreateImGuiController()
    {
        _imGuiController = new ImGuiController(
            _gl,
            s_window, // pass in our window
            _inputContext // input context
        );
    }

    private void DestroyImGuiController()
    {
        _imGuiController?.Dispose();
        _inputContext?.Dispose();
        _gl?.Dispose();
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int x)
    {
        if (key == Key.F11)
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

        if (key == Key.F12)
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
}