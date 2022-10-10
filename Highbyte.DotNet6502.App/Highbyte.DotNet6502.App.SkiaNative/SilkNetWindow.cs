using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Highbyte.DotNet6502.App.SkiaNative;
public class SilkNetWindow<TSystem>
    where TSystem : ISystem
{

    private static IWindow s_window;
    private readonly Func<SkiaRenderContext, SilkNetInputHandlerContext, SystemRunner> _getSystemRunner;
    private readonly float _canvasScale;

    // SkipSharp context/surface/canvas
    private SkiaRenderContext _skiaRenderContext;
    // SilkNet input handling
    private SilkNetInputHandlerContext _silkNetInputHandlerContext;

    // Emulator    
    private SystemRunner _systemRunner;

    // Monitor
    private SilkNetImgUIMonitor _monitor;

    public SilkNetWindow(
        IWindow window,
        Func<SkiaRenderContext, SilkNetInputHandlerContext, SystemRunner> getSystemRunner,
        float scale = 1.0f)
    {
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

        // Init Monitor ImgUI resources 
        _monitor = new SilkNetImgUIMonitor(_systemRunner.System);
        _monitor.Init(s_window, _silkNetInputHandlerContext.InputContext);
        _monitor.MonitorStateChange += (s, monitorEnabled) => _silkNetInputHandlerContext.ListenForKeyboardInput(enabled: !monitorEnabled);
    }

    protected void OnClosing()
    {
        // Cleanup Skia resources
        _skiaRenderContext.Cleanup();

        // Dispose Monitor ImgUI
        _monitor.Cleanup();

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
        if (_monitor.MonitorVisible)
        {
            return;
        }

        if (_silkNetInputHandlerContext.Exit)
        {
            s_window.Close();
            return;
        }

        // Run emulator.
        // Handle input
        _systemRunner.ProcessInput();

        // Run emulator for one frame worth of emulated CPU cycles 
        _systemRunner.RunEmulatorOneFrame();
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
        if (_monitor.MonitorVisible)
        {
            _monitor.PreOnRender(deltaTime, clearOpenGL: true);
        }

        // Render emulator system screen
        _systemRunner.Draw();

        // Flush the Skia Context
        _skiaRenderContext.GRContext?.Flush();

        // SilkNet windows are what's known as "double-buffered". In essence, the window manages two buffers.
        // One is rendered to while the other is currently displayed by the window.
        // This avoids screen tearing, a visual artifact that can happen if the buffer is modified while being displayed.
        // After drawing, call this function to swap the buffers. If you don't, it won't display what you've rendered.

        // NOTE: s_window.SwapBuffers() seem to have some problem. Window is darker, and some flickering.
        //       Use windowOptions.ShouldSwapAutomatically = true  instead
        //s_window.SwapBuffers();

        // Render monitor if enabled
        if (_monitor.MonitorVisible)
        {
            _monitor.PostOnRender(deltaTime);
        }
    }

    private void OnResize(Vector2D<int> vec2)
    {
    }
}